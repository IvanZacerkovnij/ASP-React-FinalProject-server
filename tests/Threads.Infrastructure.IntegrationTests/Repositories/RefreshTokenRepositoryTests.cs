using Microsoft.EntityFrameworkCore;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.RefreshTokens;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class RefreshTokenRepositoryTests : DatabaseTestBase
{
    public RefreshTokenRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task AddGetAndUpdate_PersistRefreshTokenState()
    {
        var user = TestEntityFactory.CreateUser();
        await SeedUsersAsync(user);
        var token = TestEntityFactory.CreateRefreshToken(user, "token-hash");
        token.User = null!;
        var revokedAt = DateTimeOffset.UtcNow;

        await using (var dbContext = Fixture.CreateContext())
        {
            var repository = new RefreshTokenRepository(dbContext);
            await repository.AddAsync(token);

            var stored = await repository.GetByTokenHashAsync(token.TokenHash);
            Assert.NotNull(stored);
            Assert.Equal(user.Username, stored.User.Username);

            stored.RevokedAt = revokedAt;
            await repository.UpdateAsync(stored);
        }

        await using var verificationContext = Fixture.CreateContext();
        var persisted = await verificationContext.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal(revokedAt, persisted.RevokedAt);
    }

    [Fact]
    public async Task UpdateAsync_WhenTrackedTokenIsStale_DoesNotOverwriteUnchangedFields()
    {
        var user = TestEntityFactory.CreateUser();
        var token = TestEntityFactory.CreateRefreshToken(user, "stale-token");
        await SeedAsync(user, token);

        await using var revocationContext = Fixture.CreateContext();
        await using var expirationContext = Fixture.CreateContext();
        var staleToken = await new RefreshTokenRepository(revocationContext).GetByIdAsync(token.Id);
        var expirationToken = await new RefreshTokenRepository(expirationContext).GetByIdAsync(token.Id);
        Assert.NotNull(staleToken);
        Assert.NotNull(expirationToken);

        var expiresAt = token.ExpiresAt.AddDays(7);
        expirationToken.ExpiresAt = expiresAt;
        await new RefreshTokenRepository(expirationContext).UpdateAsync(expirationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        staleToken.RevokedAt = revokedAt;
        await new RefreshTokenRepository(revocationContext).UpdateAsync(staleToken);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal(revokedAt, stored.RevokedAt);
        Assert.Equal(expiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task RevokeAllByUserIdAsync_RevokesOnlyActiveTokensForSpecifiedUser()
    {
        var user = TestEntityFactory.CreateUser("owner");
        var otherUser = TestEntityFactory.CreateUser("other");
        var alreadyRevokedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var activeOne = TestEntityFactory.CreateRefreshToken(user, "active-one");
        var activeTwo = TestEntityFactory.CreateRefreshToken(user, "active-two");
        var alreadyRevoked = TestEntityFactory.CreateRefreshToken(user, "already-revoked");
        alreadyRevoked.RevokedAt = alreadyRevokedAt;
        var otherToken = TestEntityFactory.CreateRefreshToken(otherUser, "other-token");
        var revokedAt = DateTimeOffset.UtcNow;

        await SeedAsync(user, otherUser, activeOne, activeTwo, alreadyRevoked, otherToken);

        await using (var dbContext = Fixture.CreateContext())
        {
            await new RefreshTokenRepository(dbContext).RevokeAllByUserIdAsync(user.Id, revokedAt);
        }

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.RefreshTokens
            .AsNoTracking()
            .ToDictionaryAsync(token => token.TokenHash);

        Assert.Equal(revokedAt, stored[activeOne.TokenHash].RevokedAt);
        Assert.Equal(revokedAt, stored[activeTwo.TokenHash].RevokedAt);
        Assert.Equal(alreadyRevokedAt, stored[alreadyRevoked.TokenHash].RevokedAt);
        Assert.Null(stored[otherToken.TokenHash].RevokedAt);
    }

    [Fact]
    public async Task TryRotateAsync_WhenNewTokenInsertFails_RollsBackRevocation()
    {
        var user = TestEntityFactory.CreateUser();
        var current = TestEntityFactory.CreateRefreshToken(user, "current-token");
        var existing = TestEntityFactory.CreateRefreshToken(user, "duplicate-token-hash");
        await SeedAsync(user, current, existing);
        var replacement = TestEntityFactory.CreateRefreshToken(user, existing.TokenHash);
        replacement.User = null!;

        await using (var dbContext = Fixture.CreateContext())
        {
            var repository = new RefreshTokenRepository(dbContext);
            await Assert.ThrowsAsync<DbUpdateException>(() => repository.TryRotateAsync(
                current.Id,
                replacement,
                DateTimeOffset.UtcNow));
        }

        await using var verificationContext = Fixture.CreateContext();
        var storedCurrent = await verificationContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.Id == current.Id);
        Assert.Null(storedCurrent.RevokedAt);
        Assert.Equal(2, await verificationContext.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ConcurrentTryRotateAsync_AllowsExactlyOneReplacement()
    {
        var user = TestEntityFactory.CreateUser();
        var current = TestEntityFactory.CreateRefreshToken(user, "current-token");
        await SeedAsync(user, current);
        var revokedAt = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(
            TryRotateAsync(current.Id, user.Id, "replacement-one", revokedAt),
            TryRotateAsync(current.Id, user.Id, "replacement-two", revokedAt));

        Assert.Single(results, wasRotated => wasRotated);
        Assert.Single(results, wasRotated => !wasRotated);

        await using var verificationContext = Fixture.CreateContext();
        var tokens = await verificationContext.RefreshTokens.AsNoTracking().ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(revokedAt, Assert.Single(tokens, token => token.Id == current.Id).RevokedAt);
        Assert.Single(tokens, token => token.Id != current.Id && token.RevokedAt is null);
    }

    [Fact]
    public async Task TryRotateAsync_WhenCurrentTokenExpired_DoesNotPersistReplacement()
    {
        var user = TestEntityFactory.CreateUser();
        var revokedAt = DateTimeOffset.UtcNow;
        var current = TestEntityFactory.CreateRefreshToken(
            user,
            "expired-token",
            revokedAt.AddSeconds(-1));
        await SeedAsync(user, current);

        var wasRotated = await TryRotateAsync(
            current.Id,
            user.Id,
            "replacement",
            revokedAt);

        Assert.False(wasRotated);
        await using var verificationContext = Fixture.CreateContext();
        var stored = Assert.Single(await verificationContext.RefreshTokens.AsNoTracking().ToListAsync());
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task TryRotateAsync_WhenCurrentTokenIsTracked_SynchronizesTrackedStateAfterCommit()
    {
        var user = TestEntityFactory.CreateUser();
        var current = TestEntityFactory.CreateRefreshToken(user, "current-token");
        await SeedAsync(user, current);
        var revokedAt = DateTimeOffset.UtcNow;

        await using var dbContext = Fixture.CreateContext();
        var repository = new RefreshTokenRepository(dbContext);
        var trackedBeforeRotation = await repository.GetByIdAsync(current.Id);
        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "replacement-token",
            ExpiresAt = revokedAt.AddDays(7),
            CreatedAt = revokedAt
        };

        var wasRotated = await repository.TryRotateAsync(
            current.Id,
            replacement,
            revokedAt);
        var trackedAfterRotation = await repository.GetByIdAsync(current.Id);

        Assert.True(wasRotated);
        Assert.Same(trackedBeforeRotation, trackedAfterRotation);
        Assert.Equal(revokedAt, trackedAfterRotation!.RevokedAt);
    }

    [Fact]
    public async Task AddAsync_WhenTokenHashIsDuplicated_EnforcesUniqueConstraint()
    {
        var user = TestEntityFactory.CreateUser();
        await SeedUsersAsync(user);
        var first = TestEntityFactory.CreateRefreshToken(user, "same-hash");
        var duplicate = TestEntityFactory.CreateRefreshToken(user, "same-hash");
        first.User = null!;
        duplicate.User = null!;

        await using var dbContext = Fixture.CreateContext();
        var repository = new RefreshTokenRepository(dbContext);
        await repository.AddAsync(first);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(duplicate));
    }

    private async Task<bool> TryRotateAsync(
        Guid currentId,
        Guid userId,
        string tokenHash,
        DateTimeOffset revokedAt)
    {
        await using var dbContext = Fixture.CreateContext();
        var replacement = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = revokedAt.AddDays(7),
            CreatedAt = revokedAt
        };

        return await new RefreshTokenRepository(dbContext).TryRotateAsync(
            currentId,
            replacement,
            revokedAt);
    }

    private async Task SeedUsersAsync(params User[] users)
    {
        await using var dbContext = Fixture.CreateContext();
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var dbContext = Fixture.CreateContext();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }
}
