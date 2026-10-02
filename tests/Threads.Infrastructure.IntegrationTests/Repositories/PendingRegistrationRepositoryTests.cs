using Microsoft.EntityFrameworkCore;
using Threads.Application.Exceptions;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.PendingRegistrations;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class PendingRegistrationRepositoryTests : DatabaseTestBase
{
    public PendingRegistrationRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Crud_UsesCaseInsensitiveLookupsAndPersistsState()
    {
        var decoy = CreateRegistration("decoy", "decoy@example.com");
        var registration = CreateRegistration("pending", "pending@example.com");

        await using var dbContext = Fixture.CreateContext();
        var repository = new PendingRegistrationRepository(dbContext);
        await repository.AddAsync(decoy);
        await repository.AddAsync(registration);

        registration.DisplayName = "Updated pending user";
        await repository.UpdateAsync(registration);

        var storedByEmail = await repository.GetByEmailAsync("PENDING@EXAMPLE.COM");
        var storedByUsername = await repository.GetByUsernameAsync("PENDING");

        Assert.Same(registration, storedByEmail);
        Assert.Equal(registration.Id, storedByUsername?.Id);
        Assert.Equal("Updated pending user", storedByEmail!.DisplayName);
        Assert.Null(await repository.GetByEmailAsync("missing@example.com"));
        Assert.Null(await repository.GetByUsernameAsync("missing"));

        await repository.DeleteAsync(registration);
        Assert.Null(await repository.GetByEmailAsync(registration.Email));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddAsync_WhenUniqueIdentityIsDuplicated_RejectsSecondRegistration(bool duplicateUsername)
    {
        var original = CreateRegistration("pending", "pending@example.com");
        var duplicate = CreateRegistration(
            duplicateUsername ? original.Username : "other",
            duplicateUsername ? "other@example.com" : original.Email);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PendingRegistrationRepository(dbContext);
        await repository.AddAsync(original);

        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(duplicate));

        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(1, await verificationContext.PendingRegistrations.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WhenTrackedRegistrationIsStale_DoesNotOverwriteUnchangedFields()
    {
        var registration = CreateRegistration("pending", "pending@example.com");
        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.PendingRegistrations.Add(registration);
            await seedContext.SaveChangesAsync();
        }

        await using var profileContext = Fixture.CreateContext();
        await using var codeContext = Fixture.CreateContext();
        var staleRegistration = await new PendingRegistrationRepository(profileContext)
            .GetByEmailAsync(registration.Email);
        var codeRegistration = await new PendingRegistrationRepository(codeContext)
            .GetByEmailAsync(registration.Email);
        Assert.NotNull(staleRegistration);
        Assert.NotNull(codeRegistration);

        codeRegistration.VerificationCode = "654321";
        await new PendingRegistrationRepository(codeContext).UpdateAsync(codeRegistration);

        staleRegistration.DisplayName = "Updated pending user";
        await new PendingRegistrationRepository(profileContext).UpdateAsync(staleRegistration);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.PendingRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal("Updated pending user", stored.DisplayName);
        Assert.Equal("654321", stored.VerificationCode);
    }

    private static PendingRegistration CreateRegistration(string username, string email)
    {
        return new PendingRegistration
        {
            Username = username,
            Email = email,
            PasswordHash = "stored-password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }
}
