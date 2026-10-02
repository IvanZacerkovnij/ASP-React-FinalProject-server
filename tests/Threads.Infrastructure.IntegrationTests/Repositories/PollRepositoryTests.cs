using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.Polls;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class PollRepositoryTests : DatabaseTestBase
{
    public PollRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetByPostIdAsync_ReturnsOptionsInPositionOrderWithVotes()
    {
        var target = await SeedPollAsync();
        await using var voteContext = Fixture.CreateContext();
        voteContext.PollVotes.Add(new PollVote
        {
            PollId = target.PollId,
            PollOptionId = target.SecondOptionId,
            UserId = target.VoterId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await voteContext.SaveChangesAsync();

        var commandCounter = new CommandCountingInterceptor();
        await using var dbContext = Fixture.CreateContext(commandCounter);
        var poll = await new PollRepository(dbContext).GetByPostIdAsync(target.PostId);

        Assert.NotNull(poll);
        Assert.Equal([target.FirstOptionId, target.SecondOptionId], poll.Options.Select(option => option.Id));
        Assert.Single(poll.Votes);
        Assert.Empty(poll.Options.First().Votes);
        Assert.Single(poll.Options.Last().Votes);
        Assert.True(commandCounter.ReaderCommandCount > 1);
    }

    [Fact]
    public async Task PollVote_WhenOptionBelongsToAnotherPoll_IsRejectedByDatabase()
    {
        var firstPoll = await SeedPollAsync("first");
        var secondPoll = await SeedPollAsync("second");

        await using var dbContext = Fixture.CreateContext();
        dbContext.PollVotes.Add(new PollVote
        {
            PollId = firstPoll.PollId,
            PollOptionId = secondPoll.FirstOptionId,
            UserId = firstPoll.VoterId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        await using var verificationContext = Fixture.CreateContext();
        Assert.Empty(await verificationContext.PollVotes.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ConcurrentTryAddVoteAsync_ForSameUser_CommitsExactlyOneVote()
    {
        var target = await SeedPollAsync();

        var results = await Task.WhenAll(
            TryAddVoteAsync(target, target.FirstOptionId),
            TryAddVoteAsync(target, target.SecondOptionId));

        Assert.Single(results, wasAdded => wasAdded);
        Assert.Single(results, wasAdded => !wasAdded);

        await using var verificationContext = Fixture.CreateContext();
        var vote = Assert.Single(await verificationContext.PollVotes.AsNoTracking().ToListAsync());
        Assert.Equal(target.PollId, vote.PollId);
        Assert.Equal(target.VoterId, vote.UserId);
    }

    private async Task<bool> TryAddVoteAsync(PollTarget target, Guid optionId)
    {
        await using var dbContext = Fixture.CreateContext();
        return await new PollRepository(dbContext).TryAddVoteAsync(new PollVote
        {
            PollId = target.PollId,
            PollOptionId = optionId,
            UserId = target.VoterId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<PollTarget> SeedPollAsync(string suffix = "")
    {
        var author = TestEntityFactory.CreateUser($"author{suffix}");
        var voter = TestEntityFactory.CreateUser($"voter{suffix}");
        var post = TestEntityFactory.CreatePost(author);
        var poll = new Poll
        {
            PostId = post.Id,
            Post = post,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var secondOption = new PollOption
        {
            PollId = poll.Id,
            Poll = poll,
            Text = "Second",
            Position = 2,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var firstOption = new PollOption
        {
            PollId = poll.Id,
            Poll = poll,
            Text = "First",
            Position = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var dbContext = Fixture.CreateContext();
        dbContext.AddRange(author, voter, post, poll, secondOption, firstOption);
        await dbContext.SaveChangesAsync();

        return new PollTarget(post.Id, poll.Id, firstOption.Id, secondOption.Id, voter.Id);
    }

    private sealed record PollTarget(
        Guid PostId,
        Guid PollId,
        Guid FirstOptionId,
        Guid SecondOptionId,
        Guid VoterId);

    private sealed class CommandCountingInterceptor : DbCommandInterceptor
    {
        public int ReaderCommandCount { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommandCount++;
            return ValueTask.FromResult(result);
        }
    }
}
