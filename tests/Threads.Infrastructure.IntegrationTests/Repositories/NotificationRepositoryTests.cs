using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Infrastructure.Data.Repositories.Notifications;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class NotificationRepositoryTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task GetByRecipientIdAsync_OrdersTiesUsesExclusiveCursorAndLimitsInDatabase()
    {
        await using var context = Fixture.CreateContext();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        var other = TestEntityFactory.CreateUser("other");
        context.Users.AddRange(actor, recipient, other);
        await context.SaveChangesAsync();
        var timestamp = DateTimeOffset.UtcNow;
        var notifications = Enumerable.Range(1, 3).Select(index => new Notification
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
            ActorId = actor.Id, RecipientId = recipient.Id, Type = NotificationType.Follow,
            SourceId = Guid.NewGuid(), CreatedAt = timestamp
        }).ToArray();
        context.Notifications.AddRange(notifications);
        context.Notifications.Add(new Notification
        {
            ActorId = actor.Id, RecipientId = other.Id, Type = NotificationType.Follow, SourceId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new NotificationRepository(context);

        var first = await repository.GetByRecipientIdAsync(recipient.Id, 1);
        Assert.Equal(new[] { notifications[2].Id, notifications[1].Id }, first.Select(item => item.Id));
        var next = await repository.GetByRecipientIdAsync(recipient.Id, 1,
            new CursorPosition(timestamp, notifications[2].Id));
        Assert.Equal(new[] { notifications[1].Id, notifications[0].Id }, next.Select(item => item.Id));
        Assert.Equal(3, await repository.GetUnreadCountAsync(recipient.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("recipient")]
    [InlineData("post")]
    [InlineData("comment")]
    [InlineData("post-author")]
    [InlineData("quote-source")]
    public async Task Queries_RecheckAvailabilityEvenIfNotificationWasInsertedDirectly(string unavailable)
    {
        await using var context = Fixture.CreateContext();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        var postAuthor = TestEntityFactory.CreateUser("post-author");
        var post = TestEntityFactory.CreatePost(postAuthor);
        var comment = TestEntityFactory.CreateComment(recipient, post);
        var source = TestEntityFactory.CreatePost(actor);
        context.AddRange(comment, source);
        await context.SaveChangesAsync();
        await context.Notifications.ExecuteDeleteAsync();
        switch (unavailable)
        {
            case "actor": actor.IsActive = false; break;
            case "recipient": recipient.DeletedAt = DateTimeOffset.UtcNow; break;
            case "post": post.DeletedAt = DateTimeOffset.UtcNow; break;
            case "comment": comment.DeletedAt = DateTimeOffset.UtcNow; break;
            case "post-author": postAuthor.IsActive = false; break;
            case "quote-source": source.DeletedAt = DateTimeOffset.UtcNow; break;
        }
        await context.SaveChangesAsync();
        var notification = new Notification
        {
            ActorId = actor.Id, RecipientId = recipient.Id, Type = NotificationType.Quote,
            SourceId = source.Id, PostId = post.Id, CommentId = comment.Id
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var repository = new NotificationRepository(context);

        Assert.Empty(await repository.GetByRecipientIdAsync(recipient.Id, 20));
        Assert.Equal(0, await repository.GetUnreadCountAsync(recipient.Id));
        Assert.Null(await repository.GetRecipientIdAsync(notification.Id));
        await repository.MarkAsReadAsync(notification.Id, recipient.Id, DateTimeOffset.UtcNow);
        await repository.MarkAllAsReadAsync(recipient.Id, DateTimeOffset.UtcNow);
        Assert.Null(await context.Notifications.Select(item => item.ReadAt).SingleAsync());
    }
}
