using Microsoft.EntityFrameworkCore;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.Bookmarks;
using Threads.Infrastructure.Data.Repositories.Likes;
using Threads.Infrastructure.Data.Repositories.Reposts;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class InteractionRepositoryTests : DatabaseTestBase
{
    public InteractionRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Theory]
    [InlineData(InteractionKind.PostLike)]
    [InlineData(InteractionKind.CommentLike)]
    [InlineData(InteractionKind.PostBookmark)]
    [InlineData(InteractionKind.CommentBookmark)]
    [InlineData(InteractionKind.PostRepost)]
    [InlineData(InteractionKind.CommentRepost)]
    public async Task ConcurrentTryAddAsync_ForSameInteraction_CommitsExactlyOneRow(
        InteractionKind interactionKind)
    {
        var target = await SeedTargetAsync();

        var results = await Task.WhenAll(
            TryAddAsync(interactionKind, target),
            TryAddAsync(interactionKind, target));

        Assert.Single(results, wasAdded => wasAdded);
        Assert.Single(results, wasAdded => !wasAdded);

        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(1, await CountAsync(verificationContext, interactionKind));
    }

    [Theory]
    [InlineData(InteractionKind.PostLike)]
    [InlineData(InteractionKind.CommentLike)]
    [InlineData(InteractionKind.PostBookmark)]
    [InlineData(InteractionKind.CommentBookmark)]
    [InlineData(InteractionKind.PostRepost)]
    [InlineData(InteractionKind.CommentRepost)]
    public async Task TryDeleteAsync_RemovesExistingInteractionOnlyOnce(InteractionKind interactionKind)
    {
        var target = await SeedTargetAsync();
        Assert.True(await TryAddAsync(interactionKind, target));

        await using var dbContext = Fixture.CreateContext();
        var firstDelete = await TryDeleteAsync(dbContext, interactionKind, target);
        var secondDelete = await TryDeleteAsync(dbContext, interactionKind, target);

        Assert.True(firstDelete);
        Assert.False(secondDelete);
        Assert.Equal(0, await CountAsync(dbContext, interactionKind));
    }

    [Theory]
    [InlineData(InteractionKind.PostLike)]
    [InlineData(InteractionKind.CommentLike)]
    [InlineData(InteractionKind.PostBookmark)]
    [InlineData(InteractionKind.CommentBookmark)]
    [InlineData(InteractionKind.PostRepost)]
    [InlineData(InteractionKind.CommentRepost)]
    public async Task TryAddAsync_WhenTargetDoesNotExist_ReturnsFalse(InteractionKind interactionKind)
    {
        var user = TestEntityFactory.CreateUser("interactor");
        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        var target = new InteractionTarget(user.Id, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(await TryAddAsync(interactionKind, target));

        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(0, await CountAsync(verificationContext, interactionKind));
    }

    private async Task<InteractionTarget> SeedTargetAsync()
    {
        var author = TestEntityFactory.CreateUser("author");
        var user = TestEntityFactory.CreateUser("interactor");
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post);

        await using var dbContext = Fixture.CreateContext();
        dbContext.AddRange(author, user, post, comment);
        await dbContext.SaveChangesAsync();

        return new InteractionTarget(user.Id, post.Id, comment.Id);
    }

    private async Task<bool> TryAddAsync(InteractionKind interactionKind, InteractionTarget target)
    {
        await using var dbContext = Fixture.CreateContext();

        return interactionKind switch
        {
            InteractionKind.PostLike => await new LikeRepository(dbContext).TryAddAsync(
                new PostLike { PostId = target.PostId, UserId = target.UserId }),
            InteractionKind.CommentLike => await new LikeRepository(dbContext).TryAddAsync(
                new CommentLike { CommentId = target.CommentId, UserId = target.UserId }),
            InteractionKind.PostBookmark => await new BookmarkRepository(dbContext).TryAddAsync(
                new PostBookmark { PostId = target.PostId, UserId = target.UserId }),
            InteractionKind.CommentBookmark => await new BookmarkRepository(dbContext).TryAddAsync(
                new CommentBookmark { CommentId = target.CommentId, UserId = target.UserId }),
            InteractionKind.PostRepost => await new RepostRepository(dbContext).TryAddAsync(
                new PostRepost { PostId = target.PostId, UserId = target.UserId }),
            InteractionKind.CommentRepost => await new RepostRepository(dbContext).TryAddAsync(
                new CommentRepost { CommentId = target.CommentId, UserId = target.UserId }),
            _ => throw new ArgumentOutOfRangeException(nameof(interactionKind), interactionKind, null)
        };
    }

    private static Task<bool> TryDeleteAsync(
        Threads.Infrastructure.Data.ThreadsDbContext dbContext,
        InteractionKind interactionKind,
        InteractionTarget target)
    {
        return interactionKind switch
        {
            InteractionKind.PostLike => new LikeRepository(dbContext).TryDeletePostAsync(target.UserId, target.PostId),
            InteractionKind.CommentLike => new LikeRepository(dbContext).TryDeleteCommentAsync(target.UserId, target.CommentId),
            InteractionKind.PostBookmark => new BookmarkRepository(dbContext).TryDeletePostAsync(target.UserId, target.PostId),
            InteractionKind.CommentBookmark => new BookmarkRepository(dbContext).TryDeleteCommentAsync(target.UserId, target.CommentId),
            InteractionKind.PostRepost => new RepostRepository(dbContext).TryDeletePostAsync(target.UserId, target.PostId),
            InteractionKind.CommentRepost => new RepostRepository(dbContext).TryDeleteCommentAsync(target.UserId, target.CommentId),
            _ => throw new ArgumentOutOfRangeException(nameof(interactionKind), interactionKind, null)
        };
    }

    private static Task<int> CountAsync(
        Threads.Infrastructure.Data.ThreadsDbContext dbContext,
        InteractionKind interactionKind)
    {
        return interactionKind switch
        {
            InteractionKind.PostLike => dbContext.PostLikes.CountAsync(),
            InteractionKind.CommentLike => dbContext.CommentLikes.CountAsync(),
            InteractionKind.PostBookmark => dbContext.PostBookmarks.CountAsync(),
            InteractionKind.CommentBookmark => dbContext.CommentBookmarks.CountAsync(),
            InteractionKind.PostRepost => dbContext.PostReposts.CountAsync(),
            InteractionKind.CommentRepost => dbContext.CommentReposts.CountAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(interactionKind), interactionKind, null)
        };
    }

    public enum InteractionKind
    {
        PostLike,
        CommentLike,
        PostBookmark,
        CommentBookmark,
        PostRepost,
        CommentRepost
    }

    private sealed record InteractionTarget(Guid UserId, Guid PostId, Guid CommentId);
}
