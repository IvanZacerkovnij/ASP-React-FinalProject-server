using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class RecommendationEndpointsTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Theory]
    [InlineData("posts")]
    [InlineData("users")]
    public async Task Recommendations_RequireAuthentication(string kind)
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/recommendations/{kind}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("posts?limit=0")]
    [InlineData("users?limit=51")]
    [InlineData("posts?cursor=invalid")]
    [InlineData("users?cursor=invalid")]
    public async Task Recommendations_InvalidRequestReturnsProblemDetails(string query)
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(viewer);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, viewer);

        var response = await client.GetAsync($"/api/recommendations/{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Posts_SnapshotPaginationIgnoresNewPostsSkipsDeletedPostsAndPreservesQuoteDto()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var viewer = TestEntityFactory.CreateUser("viewer");
        var author = TestEntityFactory.CreateUser("author");
        var first = TestEntityFactory.CreatePost(author, createdAt: now);
        var second = TestEntityFactory.CreatePost(author, createdAt: now.AddMinutes(-1));
        var third = TestEntityFactory.CreatePost(author, createdAt: now.AddMinutes(-2));
        var target = TestEntityFactory.CreateComment(author, third, createdAt: now.AddMinutes(-3));
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(viewer, author, first, second, third, target);
            context.PostReposts.Add(new PostRepost { User = author, Post = first, CreatedAt = now });
            context.PostQuotes.Add(new PostQuote
            {
                Post = first, TargetId = target.Id, TargetVersionId = target.CurrentVersionId,
                TargetType = Threads.Domain.Enums.ContentTargetType.Comment
            });
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, viewer);

        var initial = await client.GetFromJsonAsync<CursorPageResponse<PostResponse>>("/api/recommendations/posts?limit=1");
        Assert.NotNull(initial);
        Assert.Equal(first.Id, Assert.Single(initial.Items).Id);
        Assert.NotNull(initial.Items.Single().Quote?.Target);
        Assert.True(initial.HasMore);

        await using (var context = Fixture.CreateContext())
        {
            var removed = await context.Posts.FindAsync(second.Id);
            removed!.DeletedAt = DateTimeOffset.UtcNow;
            context.Posts.Add(new Post { AuthorId = author.Id, Content = "new", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
        }
        var next = await client.GetFromJsonAsync<CursorPageResponse<PostResponse>>(
            $"/api/recommendations/posts?limit=1&cursor={Uri.EscapeDataString(initial.NextCursor!)}");

        Assert.NotNull(next);
        Assert.Equal(third.Id, Assert.Single(next.Items).Id);
        Assert.False(next.HasMore);
        Assert.Null(next.NextCursor);
    }

    [Fact]
    public async Task Users_SnapshotCursorIsScopedAndNewFollowIsExcludedOnNextPage()
    {
        var viewer = TestEntityFactory.CreateUser("viewer");
        var first = TestEntityFactory.CreateUser("first", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var second = TestEntityFactory.CreateUser("second", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var third = TestEntityFactory.CreateUser("third", createdAt: DateTimeOffset.UtcNow.AddMinutes(-3));
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(viewer, first, second, third);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, viewer);
        var initial = await client.GetFromJsonAsync<CursorPageResponse<UserShortResponse>>("/api/recommendations/users?limit=1");
        Assert.NotNull(initial);
        Assert.Equal(first.Id, Assert.Single(initial.Items).Id);
        var cursor = Uri.EscapeDataString(initial.NextCursor!);

        var wrongKind = await client.GetAsync($"/api/recommendations/posts?cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongKind.StatusCode);
        Authenticate(factory, client, first);
        var wrongViewer = await client.GetAsync($"/api/recommendations/users?cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongViewer.StatusCode);
        Authenticate(factory, client, viewer);
        await using (var context = Fixture.CreateContext())
        {
            context.Follows.Add(new Follow { FollowerId = viewer.Id, FollowingId = second.Id, CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
        }

        var next = await client.GetFromJsonAsync<CursorPageResponse<UserShortResponse>>(
            $"/api/recommendations/users?limit=1&cursor={cursor}");

        Assert.NotNull(next);
        Assert.Equal(third.Id, Assert.Single(next.Items).Id);
        Assert.False(next.HasMore);
    }

    private static void Authenticate(ThreadsApiFactory factory, HttpClient client, User user)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Feed_ReturnsPublicArrayAndPersonalizesAuthenticatedRequestsWithQuoteAndViewerState()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var viewer = TestEntityFactory.CreateUser("feed-viewer");
        var author = TestEntityFactory.CreateUser("feed-author");
        var first = TestEntityFactory.CreatePost(author, createdAt: now);
        var second = TestEntityFactory.CreatePost(author, createdAt: now.AddMinutes(-1));
        var own = TestEntityFactory.CreatePost(viewer, createdAt: now);
        var old = TestEntityFactory.CreatePost(author, createdAt: now.AddDays(-31));
        var target = TestEntityFactory.CreateComment(author, second, createdAt: now);
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(viewer, author, first, second, own, old, target);
            context.PostLikes.Add(new PostLike { User = viewer, Post = first, CreatedAt = now });
            context.PostQuotes.Add(new PostQuote
            {
                Post = first, TargetId = target.Id, TargetVersionId = target.CurrentVersionId,
                TargetType = Threads.Domain.Enums.ContentTargetType.Comment
            });
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var publicFeed = await client.GetFromJsonAsync<PostResponse[]>("/api/posts/feed");
        Assert.NotNull(publicFeed);
        Assert.Equal(3, publicFeed.Length);
        Assert.Contains(publicFeed, post => post.Id == own.Id);
        Assert.All(publicFeed, post => Assert.False(post.IsLikedByCurrentUser));
        Authenticate(factory, client, viewer);
        var personalFeed = await client.GetFromJsonAsync<PostResponse[]>("/api/posts/feed");

        Assert.NotNull(personalFeed);
        Assert.Equal(2, personalFeed.Length);
        Assert.DoesNotContain(personalFeed, post => post.Id == own.Id);
        Assert.DoesNotContain(personalFeed, post => post.Id == old.Id);
        var quoted = Assert.Single(personalFeed, post => post.Id == first.Id);
        Assert.True(quoted.IsLikedByCurrentUser);
        Assert.NotNull(quoted.Quote?.Target);
    }
}
