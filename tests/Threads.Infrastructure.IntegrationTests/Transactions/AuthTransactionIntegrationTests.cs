using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Services.Auth;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.PendingRegistrations;
using Threads.Infrastructure.Data.Repositories.RefreshTokens;
using Threads.Infrastructure.Data.Repositories.Users;
using Threads.Infrastructure.Data.Transactions;
using Threads.Infrastructure.IntegrationTests.Infrastructure;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Transactions;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthTransactionIntegrationTests : DatabaseTestBase
{
    private const string ConfirmationCode = "123456";

    public AuthTransactionIntegrationTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenRefreshTokenInsertFails_RollsBackUserAndPendingRegistrationChanges()
    {
        const string collidingRefreshToken = "colliding-refresh-token";
        var existingUser = TestEntityFactory.CreateUser("existing");
        var existingToken = TestEntityFactory.CreateRefreshToken(
            existingUser,
            Hash(collidingRefreshToken));
        var pendingRegistration = new PendingRegistration
        {
            Username = "new-user",
            Email = "new-user@example.com",
            PasswordHash = "pending-password-hash",
            VerificationCode = ConfirmationCode,
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await SeedAsync(existingUser, existingToken, pendingRegistration);

        await using (var dbContext = Fixture.CreateContext())
        {
            var userRepository = new UserRepository(dbContext);
            var pendingRepository = new PendingRegistrationRepository(dbContext);
            var refreshRepository = new RefreshTokenRepository(dbContext);
            var tokenService = Substitute.For<ITokenService>();
            tokenService.GenerateRefreshToken().Returns(collidingRefreshToken);
            var refreshTokenManager = new RefreshTokenManager(refreshRepository, tokenService);
            var sessionService = new SessionService(
                userRepository,
                Substitute.For<IPasswordHasher>(),
                tokenService,
                refreshTokenManager,
                NullLogger<SessionService>.Instance);
            var registrationService = new RegistrationService(
                userRepository,
                pendingRepository,
                new AuthTransaction(dbContext),
                Substitute.For<IAuthEmailService>(),
                Substitute.For<IPasswordHasher>(),
                sessionService,
                NullLogger<RegistrationService>.Instance);

            await Assert.ThrowsAsync<DbUpdateException>(() => registrationService.VerifyEmailAsync(
                new VerifyEmailRequest
                {
                    Email = pendingRegistration.Email,
                    Code = ConfirmationCode
                }));
        }

        await using var verificationContext = Fixture.CreateContext();
        Assert.False(await verificationContext.Users.AnyAsync(user => user.Email == pendingRegistration.Email));
        Assert.True(await verificationContext.PendingRegistrations.AnyAsync(
            registration => registration.Id == pendingRegistration.Id));
        Assert.Equal(1, await verificationContext.Users.CountAsync());
        Assert.Equal(1, await verificationContext.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenSessionRevocationFails_RollsBackAllUserAndTokenChanges()
    {
        var authCodeHasher = CreateAuthCodeHasher();
        var user = TestEntityFactory.CreateUser();
        user.PendingPasswordHash = "new-password-hash";
        user.PasswordResetCodeHash = authCodeHasher.Hash(
            ConfirmationCode,
            $"password-change:{user.Id:N}");
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var token = TestEntityFactory.CreateRefreshToken(user, "active-token");
        await SeedAsync(user, token);

        await using (var dbContext = Fixture.CreateContext())
        {
            var service = CreatePasswordChangeService(dbContext, authCodeHasher);

            await Assert.ThrowsAsync<SimulatedPersistenceException>(() => service.ConfirmPasswordChangeAsync(
                user.Id,
                new ConfirmPasswordChangeRequest { Code = ConfirmationCode }));
        }

        await AssertPasswordStateWasRolledBackAsync(user, token);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenSessionRevocationFails_RollsBackAllUserAndTokenChanges()
    {
        var authCodeHasher = CreateAuthCodeHasher();
        var user = TestEntityFactory.CreateUser();
        user.PasswordResetCodeHash = authCodeHasher.Hash(
            ConfirmationCode,
            $"password-reset:{user.Id:N}");
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var token = TestEntityFactory.CreateRefreshToken(user, "active-token");
        await SeedAsync(user, token);

        await using (var dbContext = Fixture.CreateContext())
        {
            var passwordChangeService = CreatePasswordChangeService(dbContext, authCodeHasher);
            var passwordHasher = Substitute.For<IPasswordHasher>();
            passwordHasher.HashPassword("NewPassword123!").Returns("new-password-hash");
            var recoveryService = new PasswordRecoveryService(
                new UserRepository(dbContext),
                Substitute.For<IAuthEmailService>(),
                authCodeHasher,
                passwordHasher,
                passwordChangeService,
                NullLogger<PasswordRecoveryService>.Instance);

            await Assert.ThrowsAsync<SimulatedPersistenceException>(() => recoveryService.ResetPasswordAsync(
                new ResetPasswordRequest
                {
                    Email = user.Email,
                    Code = ConfirmationCode,
                    NewPassword = "NewPassword123!"
                }));
        }

        await AssertPasswordStateWasRolledBackAsync(user, token);
    }

    private PasswordChangeService CreatePasswordChangeService(
        Threads.Infrastructure.Data.ThreadsDbContext dbContext,
        IAuthCodeHasher authCodeHasher)
    {
        var realRefreshRepository = new RefreshTokenRepository(dbContext);
        var faultingRefreshRepository = Substitute.For<IRefreshTokenRepository>();
        faultingRefreshRepository
            .RevokeAllByUserIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(call => RevokeThenFailAsync(
                realRefreshRepository,
                call.ArgAt<Guid>(0),
                call.ArgAt<DateTimeOffset>(1),
                call.ArgAt<CancellationToken>(2)));

        return new PasswordChangeService(
            new UserRepository(dbContext),
            faultingRefreshRepository,
            new AuthTransaction(dbContext),
            Substitute.For<IAuthEmailService>(),
            authCodeHasher,
            Substitute.For<IPasswordHasher>(),
            NullLogger<PasswordChangeService>.Instance);
    }

    private async Task AssertPasswordStateWasRolledBackAsync(User originalUser, RefreshToken originalToken)
    {
        await using var verificationContext = Fixture.CreateContext();
        var storedUser = await verificationContext.Users.AsNoTracking().SingleAsync();
        var storedToken = await verificationContext.RefreshTokens.AsNoTracking().SingleAsync();

        Assert.Equal(originalUser.PasswordHash, storedUser.PasswordHash);
        Assert.Equal(originalUser.PendingPasswordHash, storedUser.PendingPasswordHash);
        Assert.Equal(originalUser.PasswordResetCodeHash, storedUser.PasswordResetCodeHash);
        Assert.Equal(originalUser.PasswordResetCodeExpiresAt, storedUser.PasswordResetCodeExpiresAt);
        Assert.Null(storedUser.UpdatedAt);
        Assert.Equal(originalToken.Id, storedToken.Id);
        Assert.Null(storedToken.RevokedAt);
    }

    private static async Task RevokeThenFailAsync(
        RefreshTokenRepository repository,
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await repository.RevokeAllByUserIdAsync(userId, revokedAt, cancellationToken);
        throw new SimulatedPersistenceException();
    }

    private static AuthCodeHasher CreateAuthCodeHasher()
    {
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthCodes:HashKey"] = key
            })
            .Build();

        return new AuthCodeHasher(configuration);
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var dbContext = Fixture.CreateContext();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }

    private static string Hash(string value)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class SimulatedPersistenceException : Exception;
}
