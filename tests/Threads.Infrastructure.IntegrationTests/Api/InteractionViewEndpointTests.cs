using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Security;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class InteractionViewEndpointTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Theory]
    [InlineData("posts", "like")]
    [InlineData("posts", "bookmark")]
    [InlineData("posts", "repost")]
    [InlineData("comments", "like")]
    [InlineData("comments", "bookmark")]
    [InlineData("comments", "repost")]
    public async Task AddInteraction_RecordsUniqueViewThatSurvivesRemoval(string target, string action)
    {
        var author = TestEntityFactory.CreateUser("author");
        var viewer = TestEntityFactory.CreateUser("viewer");
        var post = TestEntityFactory.CreatePost(author);
        var comment = TestEntityFactory.CreateComment(author, post);
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(author, viewer, post, comment);
            await context.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(viewer);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var targetId = target == "posts" ? post.Id : comment.Id;
        var targetUrl = $"/api/{target}/{targetId}";
        var interactionUrl = $"{targetUrl}/{action}";
        Assert.Equal(0, await GetViewsCountAsync(client, targetUrl, target));

        using var addResponse = await client.PostAsync(interactionUrl, null);
        addResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, await GetViewsCountAsync(client, targetUrl, target));

        using var viewResponse = await client.PostAsync($"{targetUrl}/view", null);
        viewResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, await GetViewsCountAsync(client, targetUrl, target));

        using var removeResponse = await client.DeleteAsync(interactionUrl);
        removeResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, await GetViewsCountAsync(client, targetUrl, target));

        using var readdResponse = await client.PostAsync(interactionUrl, null);
        readdResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, await GetViewsCountAsync(client, targetUrl, target));

        await using var verificationContext = Fixture.CreateContext();
        var storedViews = target == "posts"
            ? await verificationContext.PostViews.CountAsync(view => view.PostId == targetId && view.UserId == viewer.Id)
            : await verificationContext.CommentViews.CountAsync(view => view.CommentId == targetId && view.UserId == viewer.Id);
        Assert.Equal(1, storedViews);
    }

    private static async Task<int> GetViewsCountAsync(HttpClient client, string targetUrl, string target)
    {
        if (target == "posts")
        {
            var post = await client.GetFromJsonAsync<PostResponse>(targetUrl);
            Assert.NotNull(post);
            return post.ViewsCount;
        }

        var comment = await client.GetFromJsonAsync<CommentResponse>(targetUrl);
        Assert.NotNull(comment);
        return comment.ViewsCount;
    }
}
