using NSubstitute;
using Threads.Application.DTOs.Polls;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Interactions;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Interactions;

public class PollServiceTests
{
    private readonly IPollRepository _pollRepository = Substitute.For<IPollRepository>();
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly PollService _service;

    public PollServiceTests()
    {
        _service = new PollService(_pollRepository, _postRepository);
    }

    [Fact]
    public async Task VoteAsync_WhenPostDoesNotExist_ReturnsPostNotFound()
    {
        var postId = Guid.NewGuid();

        var result = await _service.VoteAsync(
            Guid.NewGuid(),
            postId,
            new VotePollRequest { OptionId = Guid.NewGuid() });

        Assert.Equal(PollVoteStatus.PostNotFound, result.Status);
        Assert.Null(result.Poll);
        await _pollRepository.DidNotReceive().GetByPostIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteAsync_WhenPollDoesNotExist_ReturnsPollNotFound()
    {
        var postId = Guid.NewGuid();
        _postRepository.ExistsAsync(postId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.VoteAsync(
            Guid.NewGuid(),
            postId,
            new VotePollRequest { OptionId = Guid.NewGuid() });

        Assert.Equal(PollVoteStatus.PollNotFound, result.Status);
        Assert.Null(result.Poll);
    }

    [Fact]
    public async Task VoteAsync_WhenPollIsClosed_ReturnsPollClosedWithCurrentState()
    {
        var userId = Guid.NewGuid();
        var poll = CreatePoll();
        poll.EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        ConfigureExistingPoll(poll);

        var result = await _service.VoteAsync(
            userId,
            poll.PostId,
            new VotePollRequest { OptionId = poll.Options.First().Id });

        Assert.Equal(PollVoteStatus.PollClosed, result.Status);
        Assert.NotNull(result.Poll);
        Assert.False(result.Poll.HasVotedByCurrentUser);
        await _pollRepository.DidNotReceive().TryAddVoteAsync(
            Arg.Any<PollVote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteAsync_WhenOptionDoesNotExist_ReturnsInvalidOption()
    {
        var poll = CreatePoll();
        ConfigureExistingPoll(poll);

        var result = await _service.VoteAsync(
            Guid.NewGuid(),
            poll.PostId,
            new VotePollRequest { OptionId = Guid.NewGuid() });

        Assert.Equal(PollVoteStatus.InvalidOption, result.Status);
        Assert.NotNull(result.Poll);
        await _pollRepository.DidNotReceive().TryAddVoteAsync(
            Arg.Any<PollVote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoteAsync_WhenUserAlreadyVoted_ReturnsAlreadyVoted()
    {
        var userId = Guid.NewGuid();
        var poll = CreatePoll();
        var option = poll.Options.First();
        var existingVote = new PollVote
        {
            PollId = poll.Id,
            PollOptionId = option.Id,
            UserId = userId
        };
        poll.Votes.Add(existingVote);
        option.Votes.Add(existingVote);
        ConfigureExistingPoll(poll);

        var result = await _service.VoteAsync(
            userId,
            poll.PostId,
            new VotePollRequest { OptionId = option.Id });

        Assert.Equal(PollVoteStatus.AlreadyVoted, result.Status);
        Assert.NotNull(result.Poll);
        Assert.True(result.Poll.HasVotedByCurrentUser);
        Assert.Equal(option.Id, result.Poll.SelectedOptionId);
    }

    [Fact]
    public async Task VoteAsync_WhenConcurrentInsertLoses_ReturnsPersistedPollAsAlreadyVoted()
    {
        var userId = Guid.NewGuid();
        var poll = CreatePoll();
        var option = poll.Options.First();
        var persistedPoll = CreatePoll(poll.PostId);
        var persistedOption = persistedPoll.Options.First();
        var persistedVote = new PollVote
        {
            PollId = persistedPoll.Id,
            PollOptionId = persistedOption.Id,
            UserId = userId
        };
        persistedPoll.Votes.Add(persistedVote);
        persistedOption.Votes.Add(persistedVote);

        _postRepository.ExistsAsync(poll.PostId, Arg.Any<CancellationToken>()).Returns(true);
        _pollRepository
            .GetByPostIdAsync(poll.PostId, Arg.Any<CancellationToken>())
            .Returns(poll, persistedPoll);
        _pollRepository
            .TryAddVoteAsync(Arg.Any<PollVote>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.VoteAsync(
            userId,
            poll.PostId,
            new VotePollRequest { OptionId = option.Id });

        Assert.Equal(PollVoteStatus.AlreadyVoted, result.Status);
        Assert.NotNull(result.Poll);
        Assert.Equal(persistedPoll.Id, result.Poll.Id);
        Assert.True(result.Poll.HasVotedByCurrentUser);
    }

    [Fact]
    public async Task VoteAsync_WhenVoteIsAccepted_AddsVoteAndReturnsUpdatedPoll()
    {
        var userId = Guid.NewGuid();
        var poll = CreatePoll();
        var option = poll.Options.OrderBy(item => item.Position).First();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        PollVote? addedVote = null;
        ConfigureExistingPoll(poll);
        _pollRepository
            .TryAddVoteAsync(
                Arg.Do<PollVote>(vote => addedVote = vote),
                cancellationToken)
            .Returns(true);

        var result = await _service.VoteAsync(
            userId,
            poll.PostId,
            new VotePollRequest { OptionId = option.Id },
            cancellationToken);

        Assert.Equal(PollVoteStatus.Success, result.Status);
        Assert.NotNull(result.Poll);
        Assert.NotNull(addedVote);
        Assert.Equal(poll.Id, addedVote.PollId);
        Assert.Equal(option.Id, addedVote.PollOptionId);
        Assert.Equal(userId, addedVote.UserId);
        Assert.True(result.Poll.HasVotedByCurrentUser);
        Assert.Equal(option.Id, result.Poll.SelectedOptionId);
        Assert.Equal(1, result.Poll.TotalVotes);
        Assert.Equal([0, 1], result.Poll.Options.Select(item => item.Position));
        Assert.Equal(1, result.Poll.Options.First().VotesCount);
    }

    private void ConfigureExistingPoll(Poll poll)
    {
        _postRepository.ExistsAsync(poll.PostId, Arg.Any<CancellationToken>()).Returns(true);
        _pollRepository
            .GetByPostIdAsync(poll.PostId, Arg.Any<CancellationToken>())
            .Returns(poll);
    }

    private static Poll CreatePoll(Guid? postId = null)
    {
        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            PostId = postId ?? Guid.NewGuid(),
            EndsAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        poll.Options.Add(new PollOption
        {
            Id = Guid.NewGuid(),
            PollId = poll.Id,
            Text = "First",
            Position = 0
        });
        poll.Options.Add(new PollOption
        {
            Id = Guid.NewGuid(),
            PollId = poll.Id,
            Text = "Second",
            Position = 1
        });
        return poll;
    }
}
