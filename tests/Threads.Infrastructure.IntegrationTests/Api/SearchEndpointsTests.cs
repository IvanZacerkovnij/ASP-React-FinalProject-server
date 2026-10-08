using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class SearchEndpointsTests : DatabaseTestBase
{
    private const string Email = "search-user@example.com";
    private const string Username = "search_user";
    private const string Password = "Password123!";

    public SearchEndpointsTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task UserSearch_WithFollowingFilter_ReturnsCursorEnvelope()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);

        await using (var dbContext = Fixture.CreateContext())
        {
            var current = await dbContext.Users.SingleAsync(user => user.Username == Username);
            var followed = TestEntityFactory.CreateUser("followed-search-user");
            var excluded = TestEntityFactory.CreateUser("excluded-search-user");
            dbContext.AddRange(
                followed,
                excluded,
                TestEntityFactory.CreateFollow(current, followed));
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/Search/users?people=following&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal("followed-search-user", json.GetProperty("items")[0].GetProperty("username").GetString());
        Assert.False(json.GetProperty("hasMore").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task PostSearch_WithFilters_ReturnsOnlyMatchingPostAndCursorEnvelope()
    {
        var author = TestEntityFactory.CreateUser("http-post-author");
        var matching = TestEntityFactory.CreatePost(
            author,
            "Needle exact phrase",
            new DateTimeOffset(2026, 2, 3, 12, 0, 0, TimeSpan.Zero));
        var excluded = TestEntityFactory.CreatePost(
            author,
            "Different content",
            new DateTimeOffset(2026, 2, 3, 13, 0, 0, TimeSpan.Zero));
        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(author, matching, excluded);
            await dbContext.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            "/api/Search/posts?exactPhrase=needle%20exact&from=http-post-author" +
            "&fromDate=2026-02-03&toDate=2026-02-03&hasMedia=false&limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.Equal(matching.Id, json.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.False(json.GetProperty("hasMore").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("/api/Search/users?people=everyone")]
    [InlineData("/api/Search/users?location=worldwide")]
    [InlineData("/api/Search/posts?minLikes=-1")]
    [InlineData("/api/Search/posts?fromDate=2026-02-02&toDate=2026-02-01")]
    public async Task Search_WithInvalidFilters_ReturnsBadRequest(string url)
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<AuthResponse> RegisterAndVerifyAsync(
        ThreadsApiFactory factory,
        HttpClient client)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = Username,
                Email = Email,
                Password = Password,
                DisplayName = "Search User"
            });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = Email,
                Code = factory.EmailService.GetVerificationCode(Email)
            });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var session = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(session);
        return session;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
