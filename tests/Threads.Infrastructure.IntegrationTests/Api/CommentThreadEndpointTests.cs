using System.Net;
using System.Text.Json;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class CommentThreadEndpointTests : DatabaseTestBase
{
    private static readonly DateTimeOffset FirstTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public CommentThreadEndpointTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GetThread_ForExistingComment_ReturnsContextAndPaginatedDirectReplies()
    {
        var author = TestEntityFactory.CreateUser("thread-author");
        var post = TestEntityFactory.CreatePost(author);
        var root = TestEntityFactory.CreateComment(author, post, "root", FirstTime);
        var target = TestEntityFactory.CreateComment(
            author,
            post,
            "target",
            FirstTime.AddMinutes(1),
            parentComment: root);
        var firstReply = TestEntityFactory.CreateComment(
            author,
            post,
            "first reply",
            FirstTime.AddMinutes(2),
            parentComment: target);
        var secondReply = TestEntityFactory.CreateComment(
            author,
            post,
            "second reply",
            FirstTime.AddMinutes(3),
            parentComment: target);
        var nestedReply = TestEntityFactory.CreateComment(
            author,
            post,
            "nested reply",
            FirstTime.AddMinutes(4),
            parentComment: firstReply);

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(author, post, root, target, firstReply, secondReply, nestedReply);
            await dbContext.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/comments/{target.Id}/thread?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(post.Id, body.GetProperty("post").GetProperty("id").GetGuid());
        var ancestor = Assert.Single(body.GetProperty("ancestors").EnumerateArray());
        Assert.Equal(root.Id, ancestor.GetProperty("id").GetGuid());
        Assert.Equal(target.Id, body.GetProperty("target").GetProperty("id").GetGuid());
        var replies = body.GetProperty("replies");
        var reply = Assert.Single(replies.GetProperty("items").EnumerateArray());
        Assert.Equal(firstReply.Id, reply.GetProperty("id").GetGuid());
        Assert.True(replies.GetProperty("hasMore").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(replies.GetProperty("nextCursor").GetString()));
    }

    [Fact]
    public async Task GetThread_ForUnknownComment_ReturnsProblemDetails()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var commentId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/comments/{commentId}/thread");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal("Resource not found", body.GetProperty("title").GetString());
        Assert.Equal("Comment was not found.", body.GetProperty("detail").GetString());
        Assert.Equal($"/api/comments/{commentId}/thread", body.GetProperty("instance").GetString());
        Assert.False(body.TryGetProperty("message", out _));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
