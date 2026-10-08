using System.Net;
using System.Text.Json;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepliesEndpointTests : DatabaseTestBase
{
    private static readonly DateTimeOffset FirstTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public UserRepliesEndpointTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetReplies_ForKnownUsername_ReturnsPaginatedComments()
    {
        var author = TestEntityFactory.CreateUser("reply-author");
        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var post = TestEntityFactory.CreatePost(author);
        var older = TestEntityFactory.CreateComment(author, post, "older", FirstTime);
        var newer = TestEntityFactory.CreateComment(author, post, "newer", FirstTime.AddMinutes(1));
        var otherComment = TestEntityFactory.CreateComment(
            otherAuthor,
            post,
            "must not be returned",
            FirstTime.AddMinutes(2));

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(author, otherAuthor, post, older, newer, otherComment);
            await dbContext.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/reply-author/replies?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(newer.Id, item.GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("hasMore").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("nextCursor").GetString()));
    }

    [Fact]
    public async Task GetReplies_ForUnknownUsername_ReturnsProblemDetails()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/unknown-user/replies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal("Resource not found", body.GetProperty("title").GetString());
        Assert.Equal("User was not found.", body.GetProperty("detail").GetString());
        Assert.Equal("/api/users/unknown-user/replies", body.GetProperty("instance").GetString());
        Assert.False(body.TryGetProperty("message", out _));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
