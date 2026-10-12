using Microsoft.EntityFrameworkCore;
using Threads.Application.Exceptions;
using Threads.Infrastructure.Data.Repositories.Users;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class GoogleUserRepositoryTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task LinkGoogle_UniqueSubjectCannotBeAssignedToTwoAccounts()
    {
        var first = TestEntityFactory.CreateUser("first");
        var second = TestEntityFactory.CreateUser("second");
        first.IsVerified = second.IsVerified = true;
        await using var context = Fixture.CreateContext();
        context.AddRange(first, second);
        await context.SaveChangesAsync();
        var repository = new UserRepository(context);

        Assert.True(await repository.TryLinkGoogleAsync(first.Id, "subject"));
        Assert.True(await repository.TryLinkGoogleAsync(first.Id, "subject"));
        await Assert.ThrowsAsync<ConflictException>(() => repository.TryLinkGoogleAsync(second.Id, "subject"));
        Assert.False(await repository.TryLinkGoogleAsync(first.Id, "different-subject"));
        Assert.Equal(first.Id, (await repository.GetByGoogleSubjectAsync("subject"))?.Id);
    }

    [Fact]
    public async Task AddGoogleUser_DuplicateSubjectReturnsConflict()
    {
        var first = TestEntityFactory.CreateUser("first");
        var second = TestEntityFactory.CreateUser("second");
        first.GoogleSubject = second.GoogleSubject = "subject";
        first.PasswordHash = second.PasswordHash = null;
        await using var context = Fixture.CreateContext();
        var repository = new UserRepository(context);

        await repository.AddAsync(first);
        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(second));
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task LinkGoogle_InactiveUnverifiedOrDeletedUserCannotLink(
        bool active, bool verified, bool deleted)
    {
        var user = TestEntityFactory.CreateUser();
        user.IsActive = active;
        user.IsVerified = verified;
        user.DeletedAt = deleted ? DateTimeOffset.UtcNow : null;
        await using var context = Fixture.CreateContext();
        context.Add(user);
        await context.SaveChangesAsync();

        Assert.False(await new UserRepository(context).TryLinkGoogleAsync(user.Id, "subject"));
    }

    [Fact]
    public async Task LinkGoogle_ConcurrentDifferentSubjects_OnlyOneLinkSucceeds()
    {
        var user = TestEntityFactory.CreateUser();
        user.IsVerified = true;
        await using (var context = Fixture.CreateContext())
        {
            context.Add(user);
            await context.SaveChangesAsync();
        }

        await using var firstContext = Fixture.CreateContext();
        await using var secondContext = Fixture.CreateContext();
        var results = await Task.WhenAll(
            new UserRepository(firstContext).TryLinkGoogleAsync(user.Id, "first-subject"),
            new UserRepository(secondContext).TryLinkGoogleAsync(user.Id, "second-subject"));

        Assert.Single(results, linked => linked);
        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Users.SingleAsync();
        Assert.Equal(results[0] ? "first-subject" : "second-subject", stored.GoogleSubject);
    }
}
