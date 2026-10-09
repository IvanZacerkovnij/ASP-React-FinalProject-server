using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ScheduledPostPatchEndpointTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Patch_PreservesOmittedFieldsClearsExplicitNullAndUpdatesSuppliedValues()
    {
        var viewer = TestEntityFactory.CreateUser("scheduled-patch-viewer");
        var scheduled = new ScheduledPost
        {
            Author = viewer, AuthorId = viewer.Id,
            Content = "original", ScheduledAt = new DateTimeOffset(2099, 1, 1, 12, 0, 0, TimeSpan.Zero),
            LinkPreviewUrl = "https://example.com/original"
        };
        await using (var context = Fixture.CreateContext())
        {
            context.AddRange(viewer, scheduled);
            await context.SaveChangesAsync();
        }
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(viewer));
        }
        var route = $"/api/posts/scheduled/{scheduled.Id}";

        var omitted = await client.PatchAsJsonAsync(route, new { mediaIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        var preserved = (await omitted.Content.ReadFromJsonAsync<ScheduledPostResponse>())!;
        Assert.Equal("original", preserved.Content);
        Assert.NotNull(preserved.LinkPreview);
        Assert.Equal(scheduled.ScheduledAt, preserved.ScheduledAt);

        var cleared = await client.PatchAsJsonAsync(route, new { content = (string?)null });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var withoutText = (await cleared.Content.ReadFromJsonAsync<ScheduledPostResponse>())!;
        Assert.Null(withoutText.Content);
        Assert.NotNull(withoutText.LinkPreview);

        var newTime = scheduled.ScheduledAt.AddDays(1);
        var changed = await client.PatchAsJsonAsync(route, new
        {
            content = "updated", linkPreview = (object?)null, scheduledAt = newTime
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var updated = (await changed.Content.ReadFromJsonAsync<ScheduledPostResponse>())!;
        Assert.Equal("updated", updated.Content);
        Assert.Null(updated.LinkPreview);
        Assert.Equal(newTime, updated.ScheduledAt);

        var invalid = await client.PatchAsJsonAsync(route, new { scheduledAt = (DateTimeOffset?)null });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        await using var verification = Fixture.CreateContext();
        var stored = await verification.ScheduledPosts.SingleAsync(post => post.Id == scheduled.Id);
        Assert.Equal("updated", stored.Content);
        Assert.Null(stored.LinkPreviewUrl);
        Assert.Equal(newTime, stored.ScheduledAt);
    }
}
