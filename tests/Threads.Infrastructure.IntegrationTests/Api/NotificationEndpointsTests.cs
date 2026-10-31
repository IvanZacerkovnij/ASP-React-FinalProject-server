using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class NotificationEndpointsTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Theory]
    [InlineData("GET", "/api/notifications")]
    [InlineData("GET", "/api/notifications/unread-count")]
    [InlineData("PATCH", "/api/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("PATCH", "/api/notifications/read-all")]
    public async Task Endpoints_RequireAuthentication(string method, string path)
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("cursor=invalid")]
    public async Task Get_InvalidPaginationReturnsProblemDetails(string query)
    {
        var user = TestEntityFactory.CreateUser();
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, user);

        var response = await client.GetAsync($"/api/notifications?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Endpoints_PaginateOwnNotificationsAndReadWithoutChangingOtherRecipients()
    {
        var user = TestEntityFactory.CreateUser("recipient");
        var actor = TestEntityFactory.CreateUser("actor");
        var other = TestEntityFactory.CreateUser("other");
        var post = TestEntityFactory.CreatePost(user);
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(post, actor, other);
            await context.SaveChangesAsync();
            context.Follows.AddRange(TestEntityFactory.CreateFollow(actor, user), TestEntityFactory.CreateFollow(actor, other));
            context.PostLikes.Add(TestEntityFactory.CreatePostLike(actor, post));
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, user);

        Assert.Equal(2, (await client.GetFromJsonAsync<UnreadNotificationCountResponse>("/api/notifications/unread-count"))!.UnreadCount);
        var response = await client.GetAsync("/api/notifications?limit=1");
        response.EnsureSuccessStatusCode();
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var actorJson = json.RootElement.GetProperty("items")[0].GetProperty("actor");
            Assert.False(actorJson.TryGetProperty("email", out _));
            Assert.False(actorJson.TryGetProperty("passwordHash", out _));
            Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("items")[0].GetProperty("type").ValueKind);
        }
        var first = await response.Content.ReadFromJsonAsync<CursorPageResponse<NotificationResponse>>();
        Assert.NotNull(first);
        var firstItem = Assert.Single(first.Items);
        Assert.Equal(actor.Id, firstItem.Actor.Id);
        Assert.True(first.HasMore);
        var second = await client.GetFromJsonAsync<CursorPageResponse<NotificationResponse>>(
            $"/api/notifications?limit=1&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.NotNull(second);
        Assert.NotEqual(firstItem.Id, Assert.Single(second.Items).Id);
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
        Assert.Null(firstItem.ReadAt);

        var read = await client.PatchAsync($"/api/notifications/{firstItem.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        DateTimeOffset? originalReadAt;
        Guid foreignId;
        await using (var context = Fixture.CreateContext())
        {
            originalReadAt = await context.Notifications.Where(item => item.Id == firstItem.Id).Select(item => item.ReadAt).SingleAsync();
            foreignId = await context.Notifications.Where(item => item.RecipientId == other.Id).Select(item => item.Id).SingleAsync();
        }
        Assert.NotNull(originalReadAt);
        Assert.Equal(1, (await client.GetFromJsonAsync<UnreadNotificationCountResponse>("/api/notifications/unread-count"))!.UnreadCount);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync($"/api/notifications/{firstItem.Id}/read", null)).StatusCode);
        foreach (var id in new[] { foreignId, Guid.NewGuid() })
        {
            var missing = await client.PatchAsync($"/api/notifications/{id}/read", null);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<UnreadNotificationCountResponse>("/api/notifications/unread-count"))!.UnreadCount);
        await using (var context = Fixture.CreateContext())
        {
            Assert.Equal(originalReadAt, await context.Notifications.Where(item => item.Id == firstItem.Id).Select(item => item.ReadAt).SingleAsync());
            Assert.Null(await context.Notifications.Where(item => item.Id == foreignId).Select(item => item.ReadAt).SingleAsync());
        }
    }

    [Fact]
    public async Task Get_EmptyInboxReturnsEmptyPageAndZeroUnreadCount()
    {
        var user = TestEntityFactory.CreateUser();
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        Authenticate(factory, client, user);
        var page = await client.GetFromJsonAsync<CursorPageResponse<NotificationResponse>>("/api/notifications");
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
        Assert.Equal(0, (await client.GetFromJsonAsync<UnreadNotificationCountResponse>("/api/notifications/unread-count"))!.UnreadCount);
    }

    private static void Authenticate(ThreadsApiFactory factory, HttpClient client, User user)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
