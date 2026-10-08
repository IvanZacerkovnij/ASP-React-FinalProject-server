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
        var author = TestEntityFactory.CreateUser(bio: "Author bio");
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
        Assert.Equal("Author bio", firstPage.First().Author.Bio);
    }

    [Fact]
    public async Task GetSummariesByIdsAsync_ReturnsDistinctExistingPostsInRequestedOrder()
    {
        var author = TestEntityFactory.CreateUser();
        var first = TestEntityFactory.CreatePost(author, "first");
        var second = TestEntityFactory.CreatePost(author, "second");
        var missingId = Guid.NewGuid();
        await SeedAsync(author, first, second);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);

        var result = await repository.GetSummariesByIdsAsync(
            [second.Id, missingId, first.Id, second.Id]);

        Assert.Equal([second.Id, first.Id], result.Select(post => post.Id));
    }

    [Fact]
    public async Task GetVersionsAsync_ReturnsOnlyPostVersionsInStableChronologicalOrder()
    {
        var author = TestEntityFactory.CreateUser();
        var post = TestEntityFactory.CreatePost(author);
        var otherPost = TestEntityFactory.CreatePost(author, "other");
        var versionTime = Oldest;
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        post.Versions.Add(new PostVersion
        {
            Id = higherId,
            PostId = post.Id,
            Post = post,
            SnapshotJson = "{}",
            CreatedAt = versionTime
        });
        post.Versions.Add(new PostVersion
        {
            Id = lowerId,
            PostId = post.Id,
            Post = post,
            SnapshotJson = "{}",
            CreatedAt = versionTime
        });
        otherPost.Versions.Add(new PostVersion
        {
            PostId = otherPost.Id,
            Post = otherPost,
            SnapshotJson = "{}",
            CreatedAt = versionTime.AddMinutes(-1)
        });
        await SeedAsync(author, post, otherPost);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);

        var result = await repository.GetVersionsAsync(post.Id, limit: 10);
        var afterLowerId = await repository.GetVersionsAsync(
            post.Id,
            limit: 10,
            new CursorPosition(versionTime, lowerId));

        Assert.Equal([lowerId, higherId], result.Select(version => version.Id));
        Assert.Equal([higherId], afterLowerId.Select(version => version.Id));
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

    [Fact]
    public async Task SearchAsync_AppliesAdvancedFiltersAndExcludesNonMatchingPosts()
    {
        var current = TestEntityFactory.CreateUser(
            "current-searcher",
            locationLatitude: 50.4501,
            locationLongitude: 30.5234);
        var author = TestEntityFactory.CreateUser("target-author");
        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var matching = TestEntityFactory.CreatePost(
            author,
            "The Exact Phrase contains alpha content",
            new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
            locationLatitude: 50.4547,
            locationLongitude: 30.5238);
        var excluded = TestEntityFactory.CreatePost(
            author,
            "The exact phrase contains alpha forbidden",
            new DateTimeOffset(2026, 1, 2, 11, 0, 0, TimeSpan.Zero),
            locationLatitude: 50.4547,
            locationLongitude: 30.5238);
        var wrongAuthor = TestEntityFactory.CreatePost(
            otherAuthor,
            "The exact phrase contains alpha content",
            new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero),
            locationLatitude: 50.4547,
            locationLongitude: 30.5238);
        var far = TestEntityFactory.CreatePost(
            author,
            "The exact phrase contains alpha content",
            new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero),
            locationLatitude: 49.8397,
            locationLongitude: 24.0297);
        var follow = TestEntityFactory.CreateFollow(current, author);
        var comment = TestEntityFactory.CreateComment(current, matching, "reply");
        var like = TestEntityFactory.CreatePostLike(current, matching);
        var repost = TestEntityFactory.CreatePostRepost(current, matching);
        var media = TestEntityFactory.CreatePostMedia(current, matching, "search/matching.jpg");

        await SeedAsync(
            current,
            author,
            otherAuthor,
            matching,
            excluded,
            wrongAuthor,
            far,
            follow,
            comment,
            like,
            repost,
            media);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);

        var exactPhrase = await SearchAsync(repository, exactPhrase: "exact phrase");
        var anyWords = await SearchAsync(repository, anyWords: ["missing", "alpha"]);
        var excludeWords = await SearchAsync(repository, excludeWords: ["forbidden"]);
        var from = await SearchAsync(repository, from: "target-author");
        var counters = await SearchAsync(repository, minReplies: 1, minLikes: 1, minReposts: 1);
        var dates = await SearchAsync(
            repository,
            fromDate: new DateOnly(2026, 1, 2),
            toDate: new DateOnly(2026, 1, 2));
        var withMedia = await SearchAsync(repository, hasMedia: true);
        var combined = await SearchAsync(
            repository,
            people: "following",
            location: "near",
            exactPhrase: "exact phrase",
            anyWords: ["alpha"],
            excludeWords: ["forbidden"],
            from: "target-author",
            minReplies: 1,
            minLikes: 1,
            minReposts: 1,
            fromDate: new DateOnly(2026, 1, 2),
            toDate: new DateOnly(2026, 1, 2),
            hasMedia: true,
            currentUserId: current.Id);

        Assert.Contains(exactPhrase, post => post.Id == matching.Id);
        Assert.Contains(anyWords, post => post.Id == matching.Id);
        Assert.DoesNotContain(excludeWords, post => post.Id == excluded.Id);
        Assert.DoesNotContain(from, post => post.Id == wrongAuthor.Id);
        Assert.Equal([matching.Id], counters.Select(post => post.Id));
        Assert.DoesNotContain(dates, post => post.CreatedAt.Date != matching.CreatedAt.Date);
        Assert.Equal([matching.Id], withMedia.Select(post => post.Id));
        Assert.Equal([matching.Id], combined.Select(post => post.Id));
    }

    [Fact]
    public async Task SearchAsync_FilterOnlyOrdersDescendingAndUsesExclusiveCursor()
    {
        var author = TestEntityFactory.CreateUser("ordered-author");
        var oldest = TestEntityFactory.CreatePost(author, "oldest", Oldest);
        var middle = TestEntityFactory.CreatePost(author, "middle", Middle);
        var latest = TestEntityFactory.CreatePost(author, "latest", Latest);
        await SeedAsync(author, oldest, middle, latest);

        await using var dbContext = Fixture.CreateContext();
        var repository = new PostRepository(dbContext);
        var firstPage = await SearchAsync(repository, hasMedia: false, limit: 1);
        var afterMiddle = await SearchAsync(
            repository,
            hasMedia: false,
            limit: 10,
            cursor: new CursorPosition(middle.CreatedAt, middle.Id));

        Assert.Equal([latest.Id, middle.Id], firstPage.Select(post => post.Id));
        Assert.Equal([oldest.Id], afterMiddle.Select(post => post.Id));
    }

    private static Task<IReadOnlyCollection<Threads.Application.DTOs.Posts.Models.PostSummaryReadModel>> SearchAsync(
        PostRepository repository,
        string? people = null,
        string? location = null,
        string? exactPhrase = null,
        IReadOnlyCollection<string>? anyWords = null,
        IReadOnlyCollection<string>? excludeWords = null,
        string? from = null,
        int? minReplies = null,
        int? minLikes = null,
        int? minReposts = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        bool? hasMedia = null,
        int limit = 50,
        CursorPosition? cursor = null,
        Guid? currentUserId = null)
    {
        return repository.SearchAsync(
            null,
            people,
            location,
            exactPhrase,
            anyWords ?? [],
            excludeWords ?? [],
            from,
            minReplies,
            minLikes,
            minReposts,
            fromDate,
            toDate,
            hasMedia,
            limit,
            cursor,
            currentUserId);
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
