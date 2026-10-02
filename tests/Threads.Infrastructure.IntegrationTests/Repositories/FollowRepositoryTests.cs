using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.Follows;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class FollowRepositoryTests : DatabaseTestBase
{
    private static readonly DateTimeOffset BaseTime = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    public FollowRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetFollowersAsync_OrdersDescendingAndAppliesExclusiveCursor()
    {
        var followed = TestEntityFactory.CreateUser("followed");
        var firstFollower = TestEntityFactory.CreateUser("first-follower");
        var secondFollower = TestEntityFactory.CreateUser("second-follower");
        var thirdFollower = TestEntityFactory.CreateUser("third-follower");
        var olderFollow = CreateFollow(firstFollower.Id, followed.Id, BaseTime, "00000000-0000-0000-0000-000000000010");
        var lowerLatest = CreateFollow(secondFollower.Id, followed.Id, BaseTime.AddMinutes(1), "00000000-0000-0000-0000-000000000001");
        var higherLatest = CreateFollow(thirdFollower.Id, followed.Id, BaseTime.AddMinutes(1), "00000000-0000-0000-0000-000000000002");

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.AddRange(followed, firstFollower, secondFollower, thirdFollower);
            seedContext.Follows.AddRange(olderFollow, lowerLatest, higherLatest);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new FollowRepository(dbContext);
        var firstPage = await repository.GetFollowersAsync(followed.Id, limit: 1);
        var afterHighest = await repository.GetFollowersAsync(
            followed.Id,
            limit: 1,
            new CursorPosition(higherLatest.CreatedAt, higherLatest.Id));

        Assert.Equal([higherLatest.Id, lowerLatest.Id], firstPage.Select(follow => follow.Id));
        Assert.Equal([lowerLatest.Id, olderFollow.Id], afterHighest.Select(follow => follow.Id));
        Assert.Equal(thirdFollower.Username, firstPage.First().Follower.Username);
    }

    [Fact]
    public async Task ConcurrentTryAddAsync_ForSameRelationship_CommitsExactlyOneFollow()
    {
        var follower = TestEntityFactory.CreateUser("follower");
        var followed = TestEntityFactory.CreateUser("followed");
        await SeedUsersAsync(follower, followed);

        var results = await Task.WhenAll(
            TryAddAsync(CreateFollow(follower.Id, followed.Id, BaseTime, Guid.NewGuid().ToString())),
            TryAddAsync(CreateFollow(follower.Id, followed.Id, BaseTime, Guid.NewGuid().ToString())));

        Assert.Single(results, wasAdded => wasAdded);
        Assert.Single(results, wasAdded => !wasAdded);

        await using var verificationContext = Fixture.CreateContext();
        var stored = Assert.Single(await verificationContext.Follows.AsNoTracking().ToListAsync());
        Assert.Equal(follower.Id, stored.FollowerId);
        Assert.Equal(followed.Id, stored.FollowingId);
    }

    [Fact]
    public async Task TryAddAsync_WhenUserFollowsThemself_EnforcesDatabaseCheckConstraint()
    {
        var user = TestEntityFactory.CreateUser();
        await SeedUsersAsync(user);

        await using var dbContext = Fixture.CreateContext();
        var repository = new FollowRepository(dbContext);
        var selfFollow = CreateFollow(user.Id, user.Id, BaseTime, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.TryAddAsync(selfFollow));

        await using var verificationContext = Fixture.CreateContext();
        Assert.Empty(await verificationContext.Follows.AsNoTracking().ToListAsync());
    }

    private async Task<bool> TryAddAsync(Follow follow)
    {
        await using var dbContext = Fixture.CreateContext();
        return await new FollowRepository(dbContext).TryAddAsync(follow);
    }

    private async Task SeedUsersAsync(params Threads.Domain.Entities.User[] users)
    {
        await using var dbContext = Fixture.CreateContext();
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync();
    }

    private static Follow CreateFollow(
        Guid followerId,
        Guid followingId,
        DateTimeOffset createdAt,
        string id)
    {
        return new Follow
        {
            Id = Guid.Parse(id),
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = createdAt
        };
    }
}
