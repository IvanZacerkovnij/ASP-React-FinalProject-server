using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Infrastructure.Data.Repositories.Comments;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class CommentRepositoryTests : DatabaseTestBase
{
    private static readonly DateTimeOffset FirstTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public CommentRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Crud_PersistsUpdatesAndDeletesComment()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post, "Original comment");

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(author, post);
            await dbContext.SaveChangesAsync();

            var repository = new CommentRepository(dbContext);
            await repository.AddAsync(comment);
            comment.Content = "Updated comment";
            comment.UpdatedAt = new DateTimeOffset(2026, 1, 7, 0, 0, 0, TimeSpan.Zero);
            await repository.UpdateAsync(comment);
        }

        await using (var verificationContext = Fixture.CreateContext())
        {
            var repository = new CommentRepository(verificationContext);
            var stored = await repository.GetByIdAsync(comment.Id);

            Assert.NotNull(stored);
            Assert.Equal("Updated comment", stored.Content);
            Assert.Equal(post.Id, await repository.GetPostIdByIdAsync(comment.Id));

            await repository.DeleteAsync(stored);
        }

        await using var finalContext = Fixture.CreateContext();
        Assert.False(await finalContext.Comments.AnyAsync(candidate => candidate.Id == comment.Id));
    }

    [Fact]
    public async Task GetByPostIdAsync_OrdersAscendingAndAppliesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var otherPost = TestEntityFactory.CreatePost(author, "other post");
        var lowerFirst = TestEntityFactory.CreateComment(
            author,
            post,
            "lower first",
            FirstTime,
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherFirst = TestEntityFactory.CreateComment(
            author,
            post,
            "higher first",
            FirstTime,
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var last = TestEntityFactory.CreateComment(author, post, "last", FirstTime.AddMinutes(1));
        var otherPostComment = TestEntityFactory.CreateComment(
            author,
            otherPost,
            "must not be returned",
            FirstTime.AddSeconds(-1));

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, otherPost, lowerFirst, higherFirst, last, otherPostComment);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new CommentRepository(dbContext);
        var firstPage = await repository.GetByPostIdAsync(post.Id, limit: 1);
        var afterLowerFirst = await repository.GetByPostIdAsync(
            post.Id,
            limit: 1,
            new CursorPosition(lowerFirst.CreatedAt, lowerFirst.Id));

        Assert.Equal([lowerFirst.Id, higherFirst.Id], firstPage.Select(comment => comment.Id));
        Assert.Equal([higherFirst.Id, last.Id], afterLowerFirst.Select(comment => comment.Id));
    }

    [Fact]
    public async Task UpdateAsync_WhenEntityIsDetached_PersistsProvidedState()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var original = TestEntityFactory.CreateComment(author, post, "Original comment");

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, original);
            await seedContext.SaveChangesAsync();
        }

        var updatedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var detached = new Threads.Domain.Entities.Comment
        {
            Id = original.Id,
            PostId = post.Id,
            AuthorId = author.Id,
            Content = "Detached update",
            CreatedAt = original.CreatedAt,
            UpdatedAt = updatedAt
        };

        await using (var dbContext = Fixture.CreateContext())
        {
            await new CommentRepository(dbContext).UpdateAsync(detached);
        }

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Comments.AsNoTracking().SingleAsync();
        Assert.Equal("Detached update", stored.Content);
        Assert.Equal(updatedAt, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_WhenTrackedCommentIsStale_DoesNotOverwriteUnchangedFields()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post, "Original comment");

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, comment);
            await seedContext.SaveChangesAsync();
        }

        await using var contentContext = Fixture.CreateContext();
        await using var timestampContext = Fixture.CreateContext();
        var staleComment = await new CommentRepository(contentContext).GetByIdAsync(comment.Id);
        var timestampComment = await new CommentRepository(timestampContext).GetByIdAsync(comment.Id);
        Assert.NotNull(staleComment);
        Assert.NotNull(timestampComment);

        var updatedAt = FirstTime.AddDays(1);
        timestampComment.UpdatedAt = updatedAt;
        await new CommentRepository(timestampContext).UpdateAsync(timestampComment);

        staleComment.Content = "Updated content";
        await new CommentRepository(contentContext).UpdateAsync(staleComment);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Comments.AsNoTracking().SingleAsync();
        Assert.Equal("Updated content", stored.Content);
        Assert.Equal(updatedAt, stored.UpdatedAt);
    }
}
