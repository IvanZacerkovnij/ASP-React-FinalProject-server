using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;
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
    public async Task GetByAuthorIdAsync_FiltersOrdersAndAppliesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser();
        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var post = TestEntityFactory.CreatePost(author);
        var oldest = TestEntityFactory.CreateComment(
            author,
            post,
            "oldest",
            FirstTime,
            Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var middle = TestEntityFactory.CreateComment(
            author,
            post,
            "middle",
            FirstTime.AddMinutes(1),
            Guid.Parse("00000000-0000-0000-0000-000000000020"));
        var lowerLatest = TestEntityFactory.CreateComment(
            author,
            post,
            "lower latest",
            FirstTime.AddMinutes(2),
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherLatest = TestEntityFactory.CreateComment(
            author,
            post,
            "higher latest",
            FirstTime.AddMinutes(2),
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var otherAuthorComment = TestEntityFactory.CreateComment(
            otherAuthor,
            post,
            "must not be returned",
            FirstTime.AddMinutes(3));

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(
                author,
                otherAuthor,
                post,
                oldest,
                middle,
                lowerLatest,
                higherLatest,
                otherAuthorComment);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new CommentRepository(dbContext);
        var firstPage = await repository.GetByAuthorIdAsync(author.Id, limit: 1);
        var afterHighest = await repository.GetByAuthorIdAsync(
            author.Id,
            limit: 1,
            new CursorPosition(higherLatest.CreatedAt, higherLatest.Id));
        var afterMiddle = await repository.GetByAuthorIdAsync(
            author.Id,
            limit: 2,
            new CursorPosition(middle.CreatedAt, middle.Id));

        Assert.Equal([higherLatest.Id, lowerLatest.Id], firstPage.Select(comment => comment.Id));
        Assert.Equal([lowerLatest.Id, middle.Id], afterHighest.Select(comment => comment.Id));
        Assert.Equal([oldest.Id], afterMiddle.Select(comment => comment.Id));
        Assert.DoesNotContain(firstPage, comment => comment.Id == otherAuthorComment.Id);
    }

    [Fact]
    public async Task GetThreadPartsAsync_ReturnsOrderedAncestorsAndPaginatedDirectReplies()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var root = TestEntityFactory.CreateComment(author, post, "root", FirstTime);
        var parent = TestEntityFactory.CreateComment(
            author,
            post,
            "parent",
            FirstTime.AddMinutes(1),
            parentComment: root);
        var target = TestEntityFactory.CreateComment(
            author,
            post,
            "target",
            FirstTime.AddMinutes(2),
            parentComment: parent);
        var lowerReply = TestEntityFactory.CreateComment(
            author,
            post,
            "lower reply",
            FirstTime.AddMinutes(3),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            target);
        var higherReply = TestEntityFactory.CreateComment(
            author,
            post,
            "higher reply",
            FirstTime.AddMinutes(3),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            target);
        var nestedReply = TestEntityFactory.CreateComment(
            author,
            post,
            "nested reply",
            FirstTime.AddMinutes(4),
            parentComment: lowerReply);

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, root, parent, target, lowerReply, higherReply, nestedReply);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new CommentRepository(dbContext);
        var ancestors = await repository.GetAncestorsAsync(target.Id);
        var firstPage = await repository.GetRepliesAsync(target.Id, limit: 1);
        var afterLowerReply = await repository.GetRepliesAsync(
            target.Id,
            limit: 1,
            new CursorPosition(lowerReply.CreatedAt, lowerReply.Id));

        Assert.Equal([root.Id, parent.Id], ancestors.Select(comment => comment.Id));
        Assert.Equal([lowerReply.Id, higherReply.Id], firstPage.Select(comment => comment.Id));
        Assert.Equal([higherReply.Id], afterLowerReply.Select(comment => comment.Id));
        Assert.DoesNotContain(firstPage, comment => comment.Id == nestedReply.Id);
    }

    [Fact]
    public async Task GetByPostIdAsync_OrdersAscendingAndAppliesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser();
        author.Bio = "Author bio";
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
        Assert.Equal("Author bio", firstPage.First().Author.Bio);
    }

    [Fact]
    public async Task GetSummariesByIdsAsync_ReturnsDistinctExistingCommentsInRequestedOrder()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var first = TestEntityFactory.CreateComment(author, post, "first");
        var second = TestEntityFactory.CreateComment(author, post, "second");
        var missingId = Guid.NewGuid();

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, first, second);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new CommentRepository(dbContext);

        var result = await repository.GetSummariesByIdsAsync(
            [second.Id, missingId, first.Id, second.Id]);

        Assert.Equal([second.Id, first.Id], result.Select(comment => comment.Id));
    }

    [Fact]
    public async Task GetVersionsAsync_ReturnsOnlyCommentVersionsInStableChronologicalOrder()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post);
        var otherComment = TestEntityFactory.CreateComment(author, post, "other");
        var versionTime = FirstTime;
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        comment.Versions.Add(new CommentVersion
        {
            Id = higherId,
            CommentId = comment.Id,
            Comment = comment,
            SnapshotJson = "{}",
            CreatedAt = versionTime
        });
        comment.Versions.Add(new CommentVersion
        {
            Id = lowerId,
            CommentId = comment.Id,
            Comment = comment,
            SnapshotJson = "{}",
            CreatedAt = versionTime
        });
        otherComment.Versions.Add(new CommentVersion
        {
            CommentId = otherComment.Id,
            Comment = otherComment,
            SnapshotJson = "{}",
            CreatedAt = versionTime.AddMinutes(-1)
        });

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, comment, otherComment);
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new CommentRepository(dbContext);

        var result = await repository.GetVersionsAsync(comment.Id, limit: 10);
        var afterLowerId = await repository.GetVersionsAsync(
            comment.Id,
            limit: 10,
            new CursorPosition(versionTime, lowerId));

        Assert.Equal([lowerId, higherId], result.Select(version => version.Id));
        Assert.Equal([higherId], afterLowerId.Select(version => version.Id));
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

    [Fact]
    public async Task UpdateAsync_WhenMediaConcurrencyTokenIsStaleWithoutOverlap_RetriesUpdate()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post, "Original comment");
        var media = new Media
        {
            StorageKey = "media/comment-concurrency.jpg",
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            SizeInBytes = 100,
            UploadedByUserId = author.Id,
            UploadedByUser = author,
            CommentId = comment.Id,
            Comment = comment
        };
        comment.Media.Add(media);

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, post, comment);
            await seedContext.SaveChangesAsync();
        }

        await using var staleContext = Fixture.CreateContext();
        var staleRepository = new CommentRepository(staleContext);
        var staleComment = await staleRepository.GetByIdAsync(comment.Id);
        Assert.NotNull(staleComment);
        var staleMedia = Assert.Single(staleComment.Media);

        await using (var concurrentContext = Fixture.CreateContext())
        {
            var concurrentMedia = await concurrentContext.Medias.SingleAsync(item => item.Id == media.Id);
            concurrentMedia.UpdatedAt = DateTimeOffset.UtcNow;
            await concurrentContext.SaveChangesAsync();
        }

        staleComment.Content = "Updated comment";
        staleMedia.SortOrder = 1;
        await staleRepository.UpdateAsync(staleComment);

        await using var verificationContext = Fixture.CreateContext();
        var storedComment = await verificationContext.Comments.AsNoTracking().SingleAsync(item => item.Id == comment.Id);
        var storedMedia = await verificationContext.Medias.AsNoTracking().SingleAsync(item => item.Id == media.Id);
        Assert.Equal("Updated comment", storedComment.Content);
        Assert.Equal(1, storedMedia.SortOrder);
        Assert.NotNull(storedMedia.UpdatedAt);
    }
}
