using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.Interfaces.Security;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class MediaAttachmentLimitEndpointsTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Theory]
    [InlineData("POST", "/api/posts")]
    [InlineData("PUT", "/api/posts/{id}")]
    [InlineData("POST", "/api/comments")]
    [InlineData("PUT", "/api/comments/{id}")]
    [InlineData("POST", "/api/posts/scheduled")]
    [InlineData("PATCH", "/api/posts/scheduled/{id}")]
    public async Task Requests_WithFiveMediaReturnBadRequestBeforeCreatingOrUpdatingContent(string method, string route)
    {
        var viewer = TestEntityFactory.CreateUser("media-limit-viewer");
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(viewer);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(viewer);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        using var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = JsonContent.Create(new
            {
                content = "valid content",
                postId = Guid.NewGuid(),
                mediaIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(),
                scheduledAt = DateTimeOffset.UtcNow.AddDays(1)
            })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "MediaIds", StringComparison.OrdinalIgnoreCase));
        await using var verification = Fixture.CreateContext();
        Assert.Equal(0, await verification.Posts.CountAsync());
        Assert.Equal(0, await verification.Comments.CountAsync());
        Assert.Equal(0, await verification.ScheduledPosts.CountAsync());
    }
}
