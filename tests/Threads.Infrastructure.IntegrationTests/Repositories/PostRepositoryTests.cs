using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Exceptions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Infrastructure.Data.Repositories.Media;
using Threads.Infrastructure.Data.Repositories.Posts;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostRepositoryTests : DatabaseTestBase
{
    private static readonly DateTimeOffset Oldest = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Middle = Oldest.AddMinutes(1);
    private static readonly DateTimeOffset Latest = Oldest.AddMinutes(2);

    public PostRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Crud_PersistsUpdatesAndDeletesPost()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author, "Original content");

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.Users.Add(author);
            await dbContext.SaveChangesAsync();

            var repository = new PostRepository(dbContext);
            await repository.AddAsync(post);
            post.Content = "Updated content";
            post.UpdatedAt = new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero);
            await repository.UpdateAsync(post);
        }

        await using (var verificationContext = Fixture.CreateContext())
        {
            var repository = new PostRepository(verificationContext);
            var stored = await repository.GetByIdAsync(post.Id);

            Assert.NotNull(stored);
            Assert.Equal("Updated content", stored.Content);
            Assert.Equal(post.UpdatedAt, stored.UpdatedAt);

            await repository.DeleteAsync(stored);
        }

        await using var finalContext = Fixture.CreateContext();
        Assert.False(await finalContext.Posts.AnyAsync(candidate => candidate.Id == post.Id));
    }

    [Fact]
    public async Task GetByAuthorIdAsync_OrdersByTimestampAndIdAndAppliesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser();
        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var oldest = TestEntityFactory.CreatePost(author, "oldest", Oldest, Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var middle = TestEntityFactory.CreatePost(author, "middle", Middle, Guid.Parse("00000000-0000-0000-0000-000000000020"));
        var lowerLatest = TestEntityFactory.CreatePost(author, "lower latest", Latest, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherLatest = TestEntityFactory.CreatePost(author, "higher latest", Latest, Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var otherAuthorPost = TestEntityFactory.CreatePost(otherAuthor, "must not be returned", Latest.AddMinutes(1));

        await SeedAsync(author, otherAuthor, oldest, middle, lowerLatest, higherLatest, otherAuthorPost);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);

        var firstPage = await repository.GetByAuthorIdAsync(author.Id, limit: 1);
        var afterHighest = await repository.GetByAuthorIdAsync(
            author.Id,
            limit: 1,
            new CursorPosition(higherLatest.CreatedAt, higherLatest.Id));
        var afterMiddle = await repository.GetByAuthorIdAsync(
            author.Id,
            limit: 2,
            new CursorPosition(middle.CreatedAt, middle.Id));

        Assert.Equal([higherLatest.Id, lowerLatest.Id], firstPage.Select(post => post.Id));
        Assert.Equal([lowerLatest.Id, middle.Id], afterHighest.Select(post => post.Id));
        Assert.Equal([oldest.Id], afterMiddle.Select(post => post.Id));
    }

    [Fact]
    public async Task AddAsync_WhenMediaWasAttachedByAnotherRequest_RejectsStaleAttachment()
    {
        var author = TestEntityFactory.CreateUser();
        var media = new Threads.Domain.Entities.Media
        {
            StorageKey = "media/shared.jpg",
            FileName = "shared.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 1024,
            UploadedByUserId = author.Id
        };
        await SeedAsync(author, media);

        await using var firstContext = Fixture.CreateContext();
        await using var secondContext = Fixture.CreateContext();
        var firstMedia = await new MediaRepository(firstContext).GetByIdAsync(media.Id);
        var staleMedia = await new MediaRepository(secondContext).GetByIdAsync(media.Id);
        Assert.NotNull(firstMedia);
        Assert.NotNull(staleMedia);

        var firstPost = CreatePostForExistingAuthor(author.Id, "first");
        firstMedia.PostId = firstPost.Id;
        firstPost.Media.Add(firstMedia);

        var secondPost = CreatePostForExistingAuthor(author.Id, "second");
        staleMedia.PostId = secondPost.Id;
        secondPost.Media.Add(staleMedia);

        await new PostRepository(firstContext).AddAsync(firstPost);
        await Assert.ThrowsAsync<ConflictException>(() =>
            new PostRepository(secondContext).AddAsync(secondPost));

        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(firstPost.Id, (await verificationContext.Medias.AsNoTracking().SingleAsync()).PostId);
        Assert.Equal(firstPost.Id, (await verificationContext.Posts.AsNoTracking().SingleAsync()).Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenTrackedPostIsStale_DoesNotOverwriteUnchangedFields()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author, "Original content");
        await SeedAsync(author, post);

        await using var contentContext = Fixture.CreateContext();
        await using var locationContext = Fixture.CreateContext();
        var stalePost = await new PostRepository(contentContext).GetByIdAsync(post.Id);
        var locationPost = await new PostRepository(locationContext).GetByIdAsync(post.Id);
        Assert.NotNull(stalePost);
        Assert.NotNull(locationPost);

        locationPost.LocationName = "Kyiv";
        await new PostRepository(locationContext).UpdateAsync(locationPost);

        stalePost.Content = "Updated content";
        await new PostRepository(contentContext).UpdateAsync(stalePost);

        await using var verificationContext = Fixture.CreateContext();
        var stored = await verificationContext.Posts.AsNoTracking().SingleAsync();
        Assert.Equal("Updated content", stored.Content);
        Assert.Equal("Kyiv", stored.LocationName);
    }

    [Fact]
    public async Task RecordViewAsync_WhenCalledConcurrently_IsIdempotent()
    {
        var author = TestEntityFactory.CreateUser();
        var viewer = TestEntityFactory.CreateUser("viewer");
        var post = TestEntityFactory.CreatePost(author);
        await SeedAsync(author, viewer, post);

        var counts = await Task.WhenAll(
            RecordViewAsync(post.Id, viewer.Id),
            RecordViewAsync(post.Id, viewer.Id));

        Assert.All(counts, count => Assert.Equal(1, count));

        await using var verificationContext = Fixture.CreateContext();
        var view = Assert.Single(await verificationContext.PostViews.AsNoTracking().ToListAsync());
        Assert.Equal(post.Id, view.PostId);
        Assert.Equal(viewer.Id, view.UserId);
    }

    [Fact]
    public async Task GetLikedByUserIdAsync_UsesInteractionTimeForOrderingCursorAndActionAt()
    {
        var author = TestEntityFactory.CreateUser("author");
        var user = TestEntityFactory.CreateUser("liker");
        var oldestPost = TestEntityFactory.CreatePost(author, "oldest post");
        var lowerLatestPost = TestEntityFactory.CreatePost(
            author,
            "lower latest post",
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherLatestPost = TestEntityFactory.CreatePost(
            author,
            "higher latest post",
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var oldestAction = Oldest;
        var latestAction = Oldest.AddMinutes(1);

        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.AddRange(author, user, oldestPost, lowerLatestPost, higherLatestPost);
            seedContext.PostLikes.AddRange(
                new PostLike { UserId = user.Id, PostId = oldestPost.Id, CreatedAt = oldestAction },
                new PostLike { UserId = user.Id, PostId = lowerLatestPost.Id, CreatedAt = latestAction },
                new PostLike { UserId = user.Id, PostId = higherLatestPost.Id, CreatedAt = latestAction });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);
        var firstPage = await repository.GetLikedByUserIdAsync(user.Id, limit: 1);
        var afterHighest = await repository.GetLikedByUserIdAsync(
            user.Id,
            limit: 1,
            new CursorPosition(latestAction, higherLatestPost.Id));

        Assert.Equal([higherLatestPost.Id, lowerLatestPost.Id], firstPage.Select(post => post.Id));
        Assert.All(firstPage, post => Assert.Equal(latestAction, post.ActionAt));
        Assert.Equal([lowerLatestPost.Id, oldestPost.Id], afterHighest.Select(post => post.Id));
        Assert.Equal([latestAction, oldestAction], afterHighest.Select(post => post.ActionAt));
    }

    [Fact]
    public async Task SearchAsync_UsesFullTextSearchAndAppliesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser("search-author");
        var oldest = TestEntityFactory.CreatePost(author, "integration tester oldest", Oldest);
        var middle = TestEntityFactory.CreatePost(author, "integration tester middle", Middle);
        var latest = TestEntityFactory.CreatePost(author, "integration tester latest", Latest);
        await SeedAsync(author, oldest, middle, latest);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);
        var firstPage = await repository.SearchAsync("tester", limit: 1);
        var afterMiddle = await repository.SearchAsync(
            "tester",
            limit: 2,
            new CursorPosition(middle.CreatedAt, middle.Id));

        Assert.Equal([latest.Id, middle.Id], firstPage.Select(post => post.Id));
        Assert.Equal([oldest.Id], afterMiddle.Select(post => post.Id));
    }

    private async Task<int?> RecordViewAsync(Guid postId, Guid viewerId)
    {
        await using var dbContext = Fixture.CreateContext();
        return await new PostRepository(dbContext).RecordViewAsync(postId, viewerId);
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var dbContext = Fixture.CreateContext();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }

    private static Post CreatePostForExistingAuthor(Guid authorId, string content)
    {
        return new Post
        {
            AuthorId = authorId,
            Content = content,
            CreatedAt = Latest
        };
    }
}
