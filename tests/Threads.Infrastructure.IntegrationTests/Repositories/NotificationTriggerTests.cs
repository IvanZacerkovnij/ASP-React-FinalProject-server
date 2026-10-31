using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Infrastructure.Data;
using Threads.Infrastructure.Data.Repositories.Follows;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class NotificationTriggerTests : DatabaseTestBase
{
    public NotificationTriggerTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData(NotificationType.Follow)]
    [InlineData(NotificationType.PostLike)]
    [InlineData(NotificationType.CommentLike)]
    [InlineData(NotificationType.PostRepost)]
    [InlineData(NotificationType.CommentRepost)]
    [InlineData(NotificationType.PostComment)]
    [InlineData(NotificationType.CommentReply)]
    [InlineData(NotificationType.Quote)]
    public async Task InsertAndDeleteInteraction_CreatesAndRemovesNotification(NotificationType type)
    {
        await using var context = Fixture.CreateContext();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        var post = TestEntityFactory.CreatePost(recipient);
        var comment = TestEntityFactory.CreateComment(recipient, post);
        context.AddRange(actor, comment);
        await context.SaveChangesAsync();

        object interaction = type switch
        {
            NotificationType.Follow => TestEntityFactory.CreateFollow(actor, recipient),
            NotificationType.PostLike => TestEntityFactory.CreatePostLike(actor, post),
            NotificationType.CommentLike => new CommentLike { UserId = actor.Id, CommentId = comment.Id },
            NotificationType.PostRepost => TestEntityFactory.CreatePostRepost(actor, post),
            NotificationType.CommentRepost => new CommentRepost { UserId = actor.Id, CommentId = comment.Id },
            NotificationType.PostComment => TestEntityFactory.CreateComment(actor, post),
            NotificationType.CommentReply => TestEntityFactory.CreateComment(actor, post, parentComment: comment),
            NotificationType.Quote => new PostQuote
            {
                Post = TestEntityFactory.CreatePost(actor),
                TargetType = ContentTargetType.Post,
                TargetId = post.Id,
                TargetVersionId = post.CurrentVersionId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        context.Add(interaction);
        await context.SaveChangesAsync();

        var notification = Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(type, notification.Type);
        Assert.Equal(actor.Id, notification.ActorId);
        Assert.Equal(recipient.Id, notification.RecipientId);
        Assert.Null(notification.ReadAt);
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.NotEqual(default, notification.CreatedAt);
        Assert.Equal(type == NotificationType.Follow ? (Guid?)null : post.Id, notification.PostId);
        var expectedSource = interaction switch
        {
            Follow follow => follow.Id,
            Comment createdComment => createdComment.Id,
            PostQuote quote => quote.PostId,
            CommentLike or CommentRepost => comment.Id,
            _ => post.Id
        };
        Assert.Equal(expectedSource, notification.SourceId);

        context.Remove(interaction);
        await context.SaveChangesAsync();
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplyAndCommentQuote_NotifyCommentAuthorInsteadOfPostAuthor(bool quote)
    {
        await using var context = Fixture.CreateContext();
        var postAuthor = TestEntityFactory.CreateUser("post-author");
        var commentAuthor = TestEntityFactory.CreateUser("comment-author");
        var actor = TestEntityFactory.CreateUser("actor");
        var post = TestEntityFactory.CreatePost(postAuthor);
        var parent = TestEntityFactory.CreateComment(commentAuthor, post);
        context.AddRange(actor, parent);
        await context.SaveChangesAsync();
        await context.Notifications.ExecuteDeleteAsync();

        if (quote)
        {
            context.PostQuotes.Add(new PostQuote
            {
                Post = TestEntityFactory.CreatePost(actor),
                TargetType = ContentTargetType.Comment,
                TargetId = parent.Id,
                TargetVersionId = parent.CurrentVersionId
            });
        }
        else
        {
            context.Comments.Add(TestEntityFactory.CreateComment(actor, post, parentComment: parent));
        }

        await context.SaveChangesAsync();
        var notification = Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(commentAuthor.Id, notification.RecipientId);
        Assert.Equal(quote ? NotificationType.Quote : NotificationType.CommentReply, notification.Type);
        Assert.Equal(post.Id, notification.PostId);
        if (quote)
        {
            Assert.Equal(parent.Id, notification.CommentId);
        }
    }

    [Fact]
    public async Task SelfInteractions_DoNotCreateNotifications()
    {
        await using var context = Fixture.CreateContext();
        var user = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(user);
        var comment = TestEntityFactory.CreateComment(user, post);
        context.Add(comment);
        await context.SaveChangesAsync();

        context.AddRange(
            TestEntityFactory.CreatePostLike(user, post),
            TestEntityFactory.CreatePostRepost(user, post),
            new CommentLike { UserId = user.Id, CommentId = comment.Id },
            new CommentRepost { UserId = user.Id, CommentId = comment.Id },
            TestEntityFactory.CreateComment(user, post, parentComment: comment),
            new PostQuote
            {
                Post = TestEntityFactory.CreatePost(user),
                TargetType = ContentTargetType.Post,
                TargetId = post.Id,
                TargetVersionId = post.CurrentVersionId
            });
        await context.SaveChangesAsync();
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData("actor-inactive")]
    [InlineData("recipient-deleted")]
    [InlineData("post-deleted")]
    [InlineData("comment-deleted")]
    [InlineData("post-author-inactive")]
    public async Task UnavailableUsersOrContent_DoNotCreateNotifications(string unavailable)
    {
        await using var context = Fixture.CreateContext();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        var postAuthor = TestEntityFactory.CreateUser("post-author");
        var post = TestEntityFactory.CreatePost(postAuthor);
        var comment = TestEntityFactory.CreateComment(recipient, post);
        context.AddRange(actor, comment);
        await context.SaveChangesAsync();
        await context.Notifications.ExecuteDeleteAsync();

        switch (unavailable)
        {
            case "actor-inactive": actor.IsActive = false; break;
            case "recipient-deleted": recipient.DeletedAt = DateTimeOffset.UtcNow; break;
            case "post-deleted": post.DeletedAt = DateTimeOffset.UtcNow; break;
            case "comment-deleted": comment.DeletedAt = DateTimeOffset.UtcNow; break;
            case "post-author-inactive": postAuthor.IsActive = false; break;
        }

        await context.SaveChangesAsync();
        context.CommentLikes.Add(new CommentLike { UserId = actor.Id, CommentId = comment.Id });
        await context.SaveChangesAsync();
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData("post")]
    [InlineData("comment")]
    [InlineData("actor")]
    [InlineData("recipient")]
    [InlineData("post-author")]
    [InlineData("quote-source")]
    public async Task ContentDeletionOrAccountDeactivation_RemovesExistingNotifications(string unavailable)
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
        context.PostQuotes.Add(new PostQuote
        {
            PostId = source.Id,
            TargetType = ContentTargetType.Comment,
            TargetId = comment.Id,
            TargetVersionId = comment.CurrentVersionId
        });
        await context.SaveChangesAsync();
        Assert.Equal(1, await context.Notifications.CountAsync());

        switch (unavailable)
        {
            case "post": post.DeletedAt = DateTimeOffset.UtcNow; break;
            case "comment": comment.DeletedAt = DateTimeOffset.UtcNow; break;
            case "actor": actor.IsActive = false; break;
            case "recipient": recipient.IsActive = false; break;
            case "post-author": postAuthor.IsActive = false; break;
            case "quote-source": source.DeletedAt = DateTimeOffset.UtcNow; break;
        }

        await context.SaveChangesAsync();
        Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Rollback_RemovesBothInteractionAndNotification()
    {
        await using var context = Fixture.CreateContext();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        context.Users.AddRange(actor, recipient);
        await context.SaveChangesAsync();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Follows.Add(TestEntityFactory.CreateFollow(actor, recipient));
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.Notifications.CountAsync());
            await transaction.RollbackAsync();
        }

        Assert.Empty(await context.Follows.AsNoTracking().ToListAsync());
        Assert.Empty(await context.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ConcurrentFollows_CreateOneNotification_RefollowCreatesNewUnreadNotification()
    {
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        await using var context = Fixture.CreateContext();
        context.Users.AddRange(actor, recipient);
        await context.SaveChangesAsync();

        async Task<bool> FollowAsync()
        {
            await using var concurrentContext = Fixture.CreateContext();
            return await new FollowRepository(concurrentContext).TryAddAsync(new Follow
            {
                FollowerId = actor.Id,
                FollowingId = recipient.Id
            });
        }

        var results = await Task.WhenAll(FollowAsync(), FollowAsync());
        Assert.Single(results, added => added);
        var original = Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
        await context.Notifications.ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, DateTimeOffset.UtcNow));
        await context.Follows.ExecuteDeleteAsync();
        Assert.Empty(await context.Notifications.ToListAsync());
        Assert.True(await FollowAsync());
        var renewed = Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
        Assert.NotEqual(original.Id, renewed.Id);
        Assert.Null(renewed.ReadAt);
    }

    [Fact]
    public async Task Migration_CanRollbackAndReapplyWithoutBackfillingExistingInteractions()
    {
        await using var context = Fixture.CreateContext();
        var migrator = context.GetService<IMigrator>();
        var previousMigration = (await context.Database.GetAppliedMigrationsAsync())
            .Reverse().Skip(1).First();
        var actor = TestEntityFactory.CreateUser("actor");
        var recipient = TestEntityFactory.CreateUser("recipient");
        context.Users.AddRange(actor, recipient);
        await context.SaveChangesAsync();

        try
        {
            await migrator.MigrateAsync(previousMigration);
            context.Follows.Add(TestEntityFactory.CreateFollow(actor, recipient));
            await context.SaveChangesAsync();
        }
        finally
        {
            await migrator.MigrateAsync();
        }

        Assert.Empty(await context.Notifications.AsNoTracking().ToListAsync());
        context.Follows.Add(TestEntityFactory.CreateFollow(recipient, actor));
        await context.SaveChangesAsync();
        var notification = Assert.Single(await context.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(actor.Id, notification.RecipientId);
        Assert.Equal(recipient.Id, notification.ActorId);
    }
}
