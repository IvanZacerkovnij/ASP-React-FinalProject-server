using Microsoft.Extensions.Options;
using Threads.Application.Recommendations;
using Threads.Domain.Entities;
using Threads.Infrastructure.Data.Repositories.Recommendations;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class RecommendationRepositoryTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Posts_ColdStartRanksPopularFreshPostsAndExcludesInaccessibleContent()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var firstAuthor = TestEntityFactory.CreateUser("first-author");
        var secondAuthor = TestEntityFactory.CreateUser("second-author");
        var inactive = TestEntityFactory.CreateUser("inactive");
        inactive.IsActive = false;
        var deletedUser = TestEntityFactory.CreateUser("deleted-user");
        deletedUser.DeletedAt = Now;
        var popular = TestEntityFactory.CreatePost(firstAuthor, createdAt: Now.AddHours(-1));
        var fresh = TestEntityFactory.CreatePost(secondAuthor, createdAt: Now.AddHours(-1));
        var own = TestEntityFactory.CreatePost(viewer, createdAt: Now.AddHours(-1));
        var blocked = TestEntityFactory.CreatePost(inactive, createdAt: Now.AddHours(-1));
        var deletedAuthorPost = TestEntityFactory.CreatePost(deletedUser, createdAt: Now.AddHours(-1));
        var deletedPost = TestEntityFactory.CreatePost(firstAuthor, createdAt: Now.AddHours(-1));
        deletedPost.DeletedAt = Now;
        var old = TestEntityFactory.CreatePost(secondAuthor, createdAt: Now.AddDays(-31));
        var future = TestEntityFactory.CreatePost(secondAuthor, createdAt: Now.AddHours(1));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, firstAuthor, secondAuthor, inactive, deletedUser,
            popular, fresh, own, blocked, deletedAuthorPost, deletedPost, old, future);
        context.PostLikes.Add(Like(secondAuthor, popular));
        await context.SaveChangesAsync();

        var result = await CreateRepository(context).RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { popular.Id, fresh.Id }, result);
    }

    [Fact]
    public async Task Posts_SharedPreferencesDiscoverAnotherAuthor()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var peer = TestEntityFactory.CreateUser("peer");
        var seedAuthor = TestEntityFactory.CreateUser("seed-author");
        var discoveryAuthor = TestEntityFactory.CreateUser("discovery-author");
        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var seed = TestEntityFactory.CreatePost(seedAuthor, createdAt: Now.AddDays(-40));
        var discovery = TestEntityFactory.CreatePost(discoveryAuthor, createdAt: Now.AddHours(-1));
        var other = TestEntityFactory.CreatePost(otherAuthor, createdAt: Now.AddHours(-1));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, peer, seedAuthor, discoveryAuthor, otherAuthor, seed, discovery, other);
        context.PostLikes.AddRange(Like(viewer, seed), Like(peer, seed), Like(peer, discovery), Like(seedAuthor, other));
        await context.SaveChangesAsync();

        var result = await CreateRepository(context).RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { discovery.Id, other.Id }, result);
    }

    [Fact]
    public async Task Posts_AuthorAffinityAndFollowingImproveRankAndAuthorsAreInterleaved()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var favorite = TestEntityFactory.CreateUser("favorite");
        var other = TestEntityFactory.CreateUser("other");
        var history = TestEntityFactory.CreatePost(favorite, createdAt: Now.AddDays(-40));
        var first = TestEntityFactory.CreatePost(favorite, createdAt: Now.AddHours(-1));
        var second = TestEntityFactory.CreatePost(favorite, createdAt: Now.AddHours(-2));
        var alternative = TestEntityFactory.CreatePost(other, createdAt: Now.AddMinutes(-1));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, favorite, other, history, first, second, alternative);
        context.PostLikes.Add(Like(viewer, history));
        context.Follows.Add(TestEntityFactory.CreateFollow(viewer, favorite, Now.AddDays(-1)));
        await context.SaveChangesAsync();

        var result = await CreateRepository(context).RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { first.Id, alternative.Id, second.Id }, result);
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public async Task Posts_RepeatedCommentsAreOneSignalAndViewsAreWeakerThanLikes()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var commenter = TestEntityFactory.CreateUser("commenter");
        var firstAuthor = TestEntityFactory.CreateUser("first-author");
        var secondAuthor = TestEntityFactory.CreateUser("second-author");
        var first = TestEntityFactory.CreatePost(firstAuthor, createdAt: Now.AddHours(-1));
        var second = TestEntityFactory.CreatePost(secondAuthor, createdAt: Now.AddHours(-1));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, commenter, firstAuthor, secondAuthor, first, second);
        context.Comments.Add(TestEntityFactory.CreateComment(commenter, first, createdAt: Now.AddMinutes(-5)));
        context.Comments.Add(TestEntityFactory.CreateComment(commenter, second, createdAt: Now.AddMinutes(-5)));
        context.PostViews.Add(new PostView { Post = first, User = viewer, CreatedAt = Now.AddMinutes(-5) });
        context.PostLikes.Add(Like(viewer, second));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);
        var before = await repository.RankPostsAsync(viewer.Id, Now);
        for (var index = 0; index < 50; index++)
        {
            context.Comments.Add(TestEntityFactory.CreateComment(commenter, first, createdAt: Now.AddMinutes(-4)));
        }
        await context.SaveChangesAsync();

        var after = await repository.RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { second.Id, first.Id }, before);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Posts_CommentReactionsAreSignalsButDeletedCommentsAndInactiveActorsAreIgnored()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var author = TestEntityFactory.CreateUser("author");
        var other = TestEntityFactory.CreateUser("other");
        var inactive = TestEntityFactory.CreateUser("inactive");
        inactive.IsActive = false;
        var first = TestEntityFactory.CreatePost(author, createdAt: Now.AddHours(-1));
        var second = TestEntityFactory.CreatePost(other, createdAt: Now.AddHours(-1));
        var comment = TestEntityFactory.CreateComment(other, first, createdAt: Now.AddMinutes(-5));
        var deleted = TestEntityFactory.CreateComment(author, second, createdAt: Now.AddMinutes(-5));
        deleted.DeletedAt = Now;
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, author, other, inactive, first, second, comment, deleted);
        context.CommentBookmarks.Add(new CommentBookmark { User = viewer, Comment = comment, CreatedAt = Now.AddMinutes(-4) });
        context.CommentBookmarks.Add(new CommentBookmark { User = viewer, Comment = deleted, CreatedAt = Now.AddMinutes(-4) });
        context.PostReposts.Add(new PostRepost { User = inactive, Post = second, CreatedAt = Now.AddMinutes(-4) });
        await context.SaveChangesAsync();

        var result = await CreateRepository(context).RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { first.Id, second.Id }, result);
    }

    [Fact]
    public async Task Users_RankSecondDegreeSharedFollowsAndAuthorInteractionsWithoutDuplicates()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var followed = TestEntityFactory.CreateUser("followed");
        var secondDegree = TestEntityFactory.CreateUser("second-degree");
        var shared = TestEntityFactory.CreateUser("shared");
        var interacted = TestEntityFactory.CreateUser("interacted");
        var fallback = TestEntityFactory.CreateUser("fallback");
        var inactive = TestEntityFactory.CreateUser("inactive");
        inactive.IsActive = false;
        var history = TestEntityFactory.CreatePost(interacted, createdAt: Now.AddDays(-40));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, followed, secondDegree, shared, interacted, fallback, inactive, history);
        context.Follows.AddRange(
            TestEntityFactory.CreateFollow(viewer, followed),
            TestEntityFactory.CreateFollow(followed, secondDegree),
            TestEntityFactory.CreateFollow(shared, followed),
            TestEntityFactory.CreateFollow(followed, inactive));
        context.PostLikes.Add(Like(viewer, history));
        await context.SaveChangesAsync();

        var result = (await CreateRepository(context).RankUsersAsync(viewer.Id, Now)).ToArray();

        Assert.Equal(new[] { secondDegree.Id, interacted.Id, shared.Id }, result.Take(3));
        Assert.Contains(fallback.Id, result);
        Assert.DoesNotContain(viewer.Id, result);
        Assert.DoesNotContain(followed.Id, result);
        Assert.DoesNotContain(inactive.Id, result);
        Assert.Equal(result.Length, result.Distinct().Count());
    }

    [Fact]
    public async Task Users_ColdStartUsesActivePopularityAndRevalidatesNewFollowsAndDeletion()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var popular = TestEntityFactory.CreateUser("popular");
        var follower = TestEntityFactory.CreateUser("follower");
        var deleted = TestEntityFactory.CreateUser("deleted");
        deleted.DeletedAt = Now;
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, popular, follower, deleted);
        context.Follows.Add(TestEntityFactory.CreateFollow(follower, popular));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);
        var initial = await repository.RankUsersAsync(viewer.Id, Now);
        Assert.Equal(popular.Id, initial.First());
        Assert.DoesNotContain(deleted.Id, initial);
        context.Follows.Add(TestEntityFactory.CreateFollow(viewer, popular));
        follower.IsActive = false;
        await context.SaveChangesAsync();

        var available = await repository.GetAvailableIdsAsync(viewer.Id, "users", initial);

        Assert.Empty(available);
    }

    [Fact]
    public async Task Posts_CandidateAndResultLimitsAreEnforcedWithDeterministicTies()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var author = TestEntityFactory.CreateUser("author");
        var posts = Enumerable.Range(1, 60).Select(index => TestEntityFactory.CreatePost(
            author, createdAt: Now.AddHours(-1), id: new Guid($"00000000-0000-0000-0000-{index:D12}"))).ToArray();
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, author);
        context.Posts.AddRange(posts);
        await context.SaveChangesAsync();
        var repository = CreateRepository(context, new RecommendationOptions { CandidateLimit = 50, ResultLimit = 50 });

        var first = await repository.RankPostsAsync(viewer.Id, Now);
        var second = await repository.RankPostsAsync(viewer.Id, Now);

        Assert.Equal(posts.Reverse().Take(50).Select(post => post.Id), first);
        Assert.Equal(first, second);
    }

    private static PostLike Like(User user, Post post) => new()
    {
        User = user, Post = post, CreatedAt = Now.AddMinutes(-5)
    };

    [Fact]
    public async Task Posts_FreshnessAndConfigurableWeightsChangeRanking()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var familiar = TestEntityFactory.CreateUser("familiar");
        var recentAuthor = TestEntityFactory.CreateUser("recent");
        var older = TestEntityFactory.CreatePost(familiar, createdAt: Now.AddDays(-10));
        var recent = TestEntityFactory.CreatePost(recentAuthor, createdAt: Now.AddMinutes(-1));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, familiar, recentAuthor, older, recent);
        context.Follows.Add(TestEntityFactory.CreateFollow(viewer, familiar));
        await context.SaveChangesAsync();

        var personalized = await CreateRepository(context).RankPostsAsync(viewer.Id, Now);
        var freshnessOnly = await CreateRepository(context, new RecommendationOptions
        {
            FollowWeight = 0, AuthorWeight = 0, CollaborativeWeight = 0, PopularityWeight = 0
        }).RankPostsAsync(viewer.Id, Now);

        Assert.Equal(new[] { older.Id, recent.Id }, personalized);
        Assert.Equal(new[] { recent.Id, older.Id }, freshnessOnly);
    }

    private static RecommendationRepository CreateRepository(
        Threads.Infrastructure.Data.ThreadsDbContext context, RecommendationOptions? options = null)
        => new(context, Options.Create(options ?? new RecommendationOptions()));

    [Fact]
    public async Task Feed_RandomizesRecommendedPoolPreservesAuthorDiversityAndExcludesInaccessiblePosts()
    {
        var viewer = TestEntityFactory.CreateUser("feed-viewer");
        var inactive = TestEntityFactory.CreateUser("feed-inactive");
        inactive.IsActive = false;
        var authors = Enumerable.Range(0, 12).Select(index => TestEntityFactory.CreateUser($"feed-author-{index}")).ToArray();
        var posts = authors.SelectMany(author => new[]
        {
            TestEntityFactory.CreatePost(author, createdAt: Now.AddHours(-1)),
            TestEntityFactory.CreatePost(author, createdAt: Now.AddHours(-2))
        }).ToArray();
        var own = TestEntityFactory.CreatePost(viewer, createdAt: Now.AddHours(-1));
        var blocked = TestEntityFactory.CreatePost(inactive, createdAt: Now.AddHours(-1));
        var deleted = TestEntityFactory.CreatePost(authors[0], createdAt: Now.AddHours(-1));
        deleted.DeletedAt = Now;
        var old = TestEntityFactory.CreatePost(authors[0], createdAt: Now.AddDays(-31));
        await using var context = Fixture.CreateContext();
        context.AddRange(viewer, inactive, own, blocked, deleted, old);
        context.Users.AddRange(authors);
        context.Posts.AddRange(posts);
        await context.SaveChangesAsync();
        var repository = CreateRepository(context);
        var recommendedIds = (await repository.RankPostsAsync(viewer.Id, Now)).ToHashSet();
        var authorsByPost = posts.ToDictionary(post => post.Id, post => post.AuthorId);
        var selections = new HashSet<string>();

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var feed = await repository.GetFeedIdsAsync(viewer.Id, 10, Now);
            Assert.Equal(10, feed.Count);
            Assert.Equal(10, feed.Distinct().Count());
            Assert.All(feed, id => Assert.Contains(id, recommendedIds));
            Assert.Equal(10, feed.Select(id => authorsByPost[id]).Distinct().Count());
            selections.Add(string.Join(",", feed.Order()));
        }

        Assert.True(selections.Count > 1);
        var guestFeed = await repository.GetFeedIdsAsync(null, 50, Now);
        Assert.Equal(posts.Length + 1, guestFeed.Count);
        Assert.Contains(own.Id, guestFeed);
        Assert.DoesNotContain(blocked.Id, guestFeed);
        Assert.DoesNotContain(deleted.Id, guestFeed);
        Assert.DoesNotContain(old.Id, guestFeed);
    }

    [Fact]
    public async Task Feed_SamplesOnlyConfiguredRecommendationPoolAndHandlesEmptyPool()
    {
        await using var context = Fixture.CreateContext();
        var repository = CreateRepository(context, new RecommendationOptions { CandidateLimit = 50, ResultLimit = 50 });
        Assert.Empty(await repository.GetFeedIdsAsync(null, 10, Now));
        var author = TestEntityFactory.CreateUser("author");
        var posts = Enumerable.Range(1, 60).Select(index => TestEntityFactory.CreatePost(
            author, createdAt: Now.AddHours(-1), id: new Guid($"00000000-0000-0000-0000-{index:D12}"))).ToArray();
        context.Users.Add(author);
        context.Posts.AddRange(posts);
        await context.SaveChangesAsync();
        var eligible = posts.Reverse().Take(50).Select(post => post.Id).ToHashSet();

        var feed = await repository.GetFeedIdsAsync(null, 10, Now);

        Assert.Equal(10, feed.Count);
        Assert.All(feed, id => Assert.Contains(id, eligible));
    }
}
