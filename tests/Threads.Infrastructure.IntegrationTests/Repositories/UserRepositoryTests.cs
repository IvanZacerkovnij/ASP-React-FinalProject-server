using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Exceptions;
using Threads.Infrastructure.Data.Repositories.Users;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests : DatabaseTestBase
{
    public UserRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Crud_PersistsUpdatesAndDeletesUser()
    {
        var decoy = TestEntityFactory.CreateUser("decoy", "decoy@example.com");
        var user = TestEntityFactory.CreateUser("original", "original@example.com");

        await using (var dbContext = Fixture.CreateContext())
        {
            var repository = new UserRepository(dbContext);
            await repository.AddAsync(decoy);
            await repository.AddAsync(user);

            user.DisplayName = "Updated name";
            user.UpdatedAt = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
            await repository.UpdateAsync(user);
        }

        await using (var verificationContext = Fixture.CreateContext())
        {
            var repository = new UserRepository(verificationContext);
            var storedByEmail = await repository.GetByEmailAsync("ORIGINAL@EXAMPLE.COM");
            var storedByUsername = await repository.GetByUsernameAsync("ORIGINAL");
            var storedIdByUsername = await repository.GetIdByUsernameAsync("ORIGINAL");

            Assert.NotNull(storedByEmail);
            Assert.Equal(user.Id, storedByEmail.Id);
            Assert.Equal("Updated name", storedByEmail.DisplayName);
            Assert.Equal(user.Id, storedByUsername?.Id);
            Assert.Equal(user.Id, storedIdByUsername);
            Assert.Null(await repository.GetByEmailAsync("missing@example.com"));
            Assert.Null(await repository.GetByUsernameAsync("missing"));

            await repository.DeleteAsync(storedByEmail);
        }

        await using var finalContext = Fixture.CreateContext();
        Assert.False(await finalContext.Users.AnyAsync(candidate => candidate.Id == user.Id));
    }

    [Fact]
    public async Task GetByIdAsync_LoadsOnlyUserWhileRelationQueryLoadsRequiredCollections()
    {
        var user = TestEntityFactory.CreateUser("owner");
        var following = TestEntityFactory.CreateUser("following");
        var follower = TestEntityFactory.CreateUser("follower");
        var post = TestEntityFactory.CreatePost(user);
        var followingRelation = new Threads.Domain.Entities.Follow
        {
            FollowerId = user.Id,
            FollowingId = following.Id
        };
        var followerRelation = new Threads.Domain.Entities.Follow
        {
            FollowerId = follower.Id,
            FollowingId = user.Id
        };

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(user, following, follower, post, followingRelation, followerRelation);
            await seedContext.SaveChangesAsync();
        }

        await using (var minimalContext = Fixture.CreateContext())
        {
            var stored = await new UserRepository(minimalContext).GetByIdAsync(user.Id);

            Assert.NotNull(stored);
            Assert.False(minimalContext.Entry(stored).Collection(candidate => candidate.Posts).IsLoaded);
            Assert.False(minimalContext.Entry(stored).Collection(candidate => candidate.FollowingRelations).IsLoaded);
            Assert.False(minimalContext.Entry(stored).Collection(candidate => candidate.FollowerRelations).IsLoaded);
        }

        await using var relationsContext = Fixture.CreateContext();
        var storedWithRelations = await new UserRepository(relationsContext)
            .GetWithRelationsByIdAsync(user.Id);

        Assert.NotNull(storedWithRelations);
        Assert.Equal(post.Id, Assert.Single(storedWithRelations.Posts).Id);
        Assert.Equal(following.Id, Assert.Single(storedWithRelations.FollowingRelations).FollowingId);
        Assert.Equal(follower.Id, Assert.Single(storedWithRelations.FollowerRelations).FollowerId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddAsync_WhenUniqueIdentityIsDuplicated_LeavesOnlyOriginalUser(bool duplicateUsername)
    {
        var original = TestEntityFactory.CreateUser("unique-user", "unique@example.com");
        var duplicate = TestEntityFactory.CreateUser(
            duplicateUsername ? original.Username : "another-user",
            duplicateUsername ? "another@example.com" : original.Email);

        await using var dbContext = Fixture.CreateContext();
        var repository = new UserRepository(dbContext);
        await repository.AddAsync(original);

        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(duplicate));

        await using var verificationContext = Fixture.CreateContext();
        var users = await verificationContext.Users.AsNoTracking().ToListAsync();
        var stored = Assert.Single(users);
        Assert.Equal(original.Id, stored.Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenTrackedUserIsStale_DoesNotOverwriteUnchangedSecurityFields()
    {
        var user = TestEntityFactory.CreateUser();
        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        await using var profileContext = Fixture.CreateContext();
        await using var securityContext = Fixture.CreateContext();
        var staleProfileUser = await new UserRepository(profileContext).GetByIdAsync(user.Id);
        var securityUser = await new UserRepository(securityContext).GetByIdAsync(user.Id);
        Assert.NotNull(staleProfileUser);
        Assert.NotNull(securityUser);

        securityUser.PasswordHash = "new-password-hash";
        securityUser.PasswordResetCodeHash = "new-reset-code-hash";
        await new UserRepository(securityContext).UpdateAsync(securityUser);

        staleProfileUser.DisplayName = "Updated profile";
        await new UserRepository(profileContext).UpdateAsync(staleProfileUser);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal("Updated profile", stored.DisplayName);
        Assert.Equal("new-password-hash", stored.PasswordHash);
        Assert.Equal("new-reset-code-hash", stored.PasswordResetCodeHash);
    }

    [Fact]
    public async Task ConcurrentAdds_WithSameUsername_CommitExactlyOneUser()
    {
        var first = TestEntityFactory.CreateUser("contended", "first@example.com");
        var second = TestEntityFactory.CreateUser("contended", "second@example.com");

        var results = await Task.WhenAll(TryAddAsync(first), TryAddAsync(second));

        Assert.Single(results, wasAdded => wasAdded);
        Assert.Single(results, wasAdded => !wasAdded);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Users
            .AsNoTracking()
            .Where(user => user.Username == "contended")
            .ToListAsync();
        Assert.Single(stored);
    }

    [Fact]
    public async Task SearchAsync_UsesFullTextSearchAndAppliesTextCursorOrdering()
    {
        var lowerAlpha = TestEntityFactory.CreateUser(
            "alpha-a",
            "alpha-one@example.com",
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherAlpha = TestEntityFactory.CreateUser(
            "alpha-b",
            "alpha-two@example.com",
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var beta = TestEntityFactory.CreateUser("beta");
        lowerAlpha.DisplayName = "Integration tester";
        higherAlpha.DisplayName = "Integration tester";
        beta.DisplayName = "Integration tester";

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.AddRange(lowerAlpha, higherAlpha, beta);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new UserRepository(dbContext);
        var firstPage = await repository.SearchAsync("tester", limit: 1);
        var afterLowerAlpha = await repository.SearchAsync(
            "tester",
            limit: 1,
            new TextCursorPosition(lowerAlpha.Username, lowerAlpha.Id));

        Assert.Equal([lowerAlpha.Id, higherAlpha.Id], firstPage.Select(user => user.Id));
        Assert.Equal([higherAlpha.Id, beta.Id], afterLowerAlpha.Select(user => user.Id));
    }

    [Fact]
    public async Task SearchAsync_FiltersFollowingAndSupportsFilterOnlySearch()
    {
        var current = TestEntityFactory.CreateUser("current");
        var followed = TestEntityFactory.CreateUser("alpha-followed");
        var notFollowed = TestEntityFactory.CreateUser("beta-not-followed");
        var follow = TestEntityFactory.CreateFollow(current, followed);

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(current, followed, notFollowed, follow);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var result = await new UserRepository(dbContext).SearchAsync(
            null,
            "following",
            null,
            10,
            currentUserId: current.Id);

        Assert.Equal([followed.Id], result.Select(user => user.Id));
    }

    [Fact]
    public async Task SearchAsync_FiltersNearUsersAndKeepsUsernameCursorOrdering()
    {
        var current = TestEntityFactory.CreateUser(
            "current",
            locationLatitude: 50.4501,
            locationLongitude: 30.5234);
        var nearAlpha = TestEntityFactory.CreateUser(
            "alpha-near",
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            locationLatitude: 50.4547,
            locationLongitude: 30.5238);
        var nearBeta = TestEntityFactory.CreateUser(
            "beta-near",
            locationLatitude: 50.4017,
            locationLongitude: 30.2525);
        var far = TestEntityFactory.CreateUser(
            "far-away",
            locationLatitude: 49.8397,
            locationLongitude: 24.0297);
        var missingCoordinates = TestEntityFactory.CreateUser("missing-coordinates");

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(current, nearAlpha, nearBeta, far, missingCoordinates);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new UserRepository(dbContext);
        var firstPage = await repository.SearchAsync(
            null,
            null,
            "near",
            1,
            currentUserId: current.Id);
        var afterAlpha = await repository.SearchAsync(
            null,
            null,
            "near",
            10,
            new TextCursorPosition(nearAlpha.Username, nearAlpha.Id),
            current.Id);

        Assert.Equal([nearAlpha.Id, nearBeta.Id], firstPage.Select(user => user.Id));
        Assert.Equal([nearBeta.Id, current.Id], afterAlpha.Select(user => user.Id));
        Assert.DoesNotContain(firstPage, user => user.Id == far.Id || user.Id == missingCoordinates.Id);
    }

    [Fact]
    public async Task CaseInsensitiveLookups_AreIndependentOfCurrentCulture()
    {
        var user = TestEntityFactory.CreateUser("identity", "identity@example.com");
        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            await using var dbContext = Fixture.CreateContext();
            var repository = new UserRepository(dbContext);
            var storedByEmail = await repository.GetByEmailAsync("IDENTITY@EXAMPLE.COM");
            var storedByUsername = await repository.GetByUsernameAsync("IDENTITY");

            Assert.Equal(user.Id, storedByEmail?.Id);
            Assert.Equal(user.Id, storedByUsername?.Id);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private async Task<bool> TryAddAsync(Threads.Domain.Entities.User user)
    {
        await using var dbContext = Fixture.CreateContext();
        var repository = new UserRepository(dbContext);

        try
        {
            await repository.AddAsync(user);
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }
}
