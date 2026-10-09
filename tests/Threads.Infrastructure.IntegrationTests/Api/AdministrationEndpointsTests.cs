using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Threads.Application.Interfaces.Security;
using Threads.Application.DTOs.Users;
using Threads.Domain.Enums;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AdministrationEndpointsTests : DatabaseTestBase
{
    public AdministrationEndpointsTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task OpenApi_ContainsAdministrationAndReportContracts()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/admin/users", out _));
        Assert.True(paths.TryGetProperty("/api/admin/moderation/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/admin/dashboard/metrics", out _));
        Assert.True(paths.TryGetProperty("/api/posts/{id}/report", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("ProblemDetails", out _));

        var adminResponses = paths.GetProperty("/api/admin/users")
            .GetProperty("get")
            .GetProperty("responses");
        Assert.True(adminResponses.TryGetProperty("401", out _));
        Assert.True(adminResponses.TryGetProperty("403", out _));

        var reportResponses = paths.GetProperty("/api/posts/{id}/report")
            .GetProperty("post")
            .GetProperty("responses");
        Assert.True(reportResponses.TryGetProperty("400", out _));
        Assert.True(reportResponses.TryGetProperty("401", out _));
        Assert.True(reportResponses.TryGetProperty("404", out _));
    }

    [Fact]
    public async Task ReportModerateAndDashboard_EnforceAccessAndPersistAudit()
    {
        var admin = TestEntityFactory.CreateAdmin("admin", "admin@example.com");
        var reporter = TestEntityFactory.CreateUser("reporter", "reporter@example.com");
        var author = TestEntityFactory.CreateUser("author", "author@example.com");
        var post = TestEntityFactory.CreatePost(author, "reported content");

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(admin, reporter, author, post);
            await dbContext.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var anonymousClient = factory.CreateClient();
        var unauthorized = await anonymousClient.GetAsync("/api/admin/dashboard/metrics");
        await AssertProblemDetailsAsync(
            unauthorized,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Authentication is required.",
            "/api/admin/dashboard/metrics");

        using var userClient = CreateAuthenticatedClient(factory, reporter);
        var forbidden = await userClient.GetAsync("/api/admin/dashboard/metrics");
        await AssertProblemDetailsAsync(
            forbidden,
            HttpStatusCode.Forbidden,
            "Forbidden",
            "Administrator access is required.",
            "/api/admin/dashboard/metrics");

        var invalidReason = await userClient.PostAsJsonAsync(
            $"/api/posts/{post.Id}/report",
            new { reason = "unknown" });
        await AssertProblemDetailsAsync(
            invalidReason,
            HttpStatusCode.BadRequest,
            "Invalid request",
            "Unknown report reason.",
            $"/api/posts/{post.Id}/report");

        var selfReport = await userClient.PostAsJsonAsync(
            $"/api/users/{reporter.Id}/report",
            new { reason = "spam" });
        Assert.Equal(HttpStatusCode.BadRequest, selfReport.StatusCode);

        var created = await userClient.PostAsJsonAsync(
            $"/api/posts/{post.Id}/report",
            new { reason = "spam" });
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);

        Guid reportId;
        await using (var dbContext = Fixture.CreateContext())
        {
            var report = await dbContext.Reports.AsNoTracking().SingleAsync();
            reportId = report.Id;
            Assert.Equal(ReportSource.User, report.Source);
            Assert.Equal(ReportStatus.Pending, report.Status);
            Assert.Equal(reporter.Id, report.ReporterId);
        }

        using var adminClient = CreateAuthenticatedClient(factory, admin);
        var me = await adminClient.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using (var document = JsonDocument.Parse(await me.Content.ReadAsStringAsync()))
        {
            Assert.Equal(UserRoleContract.Admin, document.RootElement.GetProperty("role").GetString());
        }

        var list = await adminClient.GetAsync("/api/admin/moderation?status=pending&type=posts");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Single(root.GetProperty("items").EnumerateArray());
            Assert.Equal(1, root.GetProperty("pagination").GetProperty("total").GetInt32());
            Assert.Equal("posts", root.GetProperty("items")[0].GetProperty("targetType").GetString());
            Assert.Equal("reported content", root.GetProperty("items")[0]
                .GetProperty("target").GetProperty("data").GetProperty("content").GetString());
        }

        var resolved = await adminClient.PutAsJsonAsync(
            $"/api/admin/moderation/{reportId}/status",
            new { status = "resolved", decision = "deleted" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using (var document = JsonDocument.Parse(await resolved.Content.ReadAsStringAsync()))
        {
            Assert.Equal("resolved", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("deleted", document.RootElement.GetProperty("decision").GetString());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("target").ValueKind);
            Assert.Equal(admin.Id, document.RootElement.GetProperty("resolvedBy").GetProperty("id").GetGuid());
        }

        var repeated = await adminClient.PutAsJsonAsync(
            $"/api/admin/moderation/{reportId}/status",
            new { status = "resolved", decision = "deleted" });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        var conflicting = await adminClient.PutAsJsonAsync(
            $"/api/admin/moderation/{reportId}/status",
            new { status = "resolved", decision = "kept" });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var hiddenPost = await anonymousClient.GetAsync($"/api/posts/{post.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPost.StatusCode);

        var charts = await adminClient.GetAsync("/api/admin/dashboard/charts?period=7d");
        Assert.Equal(HttpStatusCode.OK, charts.StatusCode);
        using (var document = JsonDocument.Parse(await charts.Content.ReadAsStringAsync()))
        {
            Assert.Equal(7, document.RootElement.GetProperty("audience").GetProperty("7d").GetArrayLength());
            Assert.Equal(30, document.RootElement.GetProperty("activity").GetProperty("30d")
                .GetProperty("posts").GetArrayLength());
        }

        var tables = await adminClient.GetAsync("/api/admin/dashboard/tables?limit=5");
        Assert.Equal(HttpStatusCode.OK, tables.StatusCode);
        using (var document = JsonDocument.Parse(await tables.Content.ReadAsStringAsync()))
        {
            Assert.Equal(3, document.RootElement.GetProperty("latestUsers").GetArrayLength());
            Assert.Single(document.RootElement.GetProperty("latestReports").EnumerateArray());
        }
    }

    [Fact]
    public async Task AdminUsers_BlockIsIdempotentSelfActionForbiddenAndTokenStopsWorking()
    {
        var admin = TestEntityFactory.CreateAdmin("admin", "admin@example.com");
        var user = TestEntityFactory.CreateUser("ordinary", "ordinary@example.com");

        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(admin, user);
            await dbContext.SaveChangesAsync();
        }

        using var factory = new ThreadsApiFactory(Fixture);
        using var adminClient = CreateAuthenticatedClient(factory, admin);
        using var userClient = CreateAuthenticatedClient(factory, user);

        var selfBlock = await adminClient.PutAsync($"/api/admin/users/{admin.Id}/block", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfBlock.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await adminClient.PutAsync($"/api/admin/users/{user.Id}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await adminClient.PutAsync($"/api/admin/users/{user.Id}/block", null)).StatusCode);

        var blockedToken = await userClient.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, blockedToken.StatusCode);

        var users = await adminClient.GetAsync("/api/admin/users?status=blocked&search=ordinary&page=99");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        using var document = JsonDocument.Parse(await users.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("pagination").GetProperty("page").GetInt32());
        Assert.True(document.RootElement.GetProperty("items")[0].GetProperty("isBlocked").GetBoolean());
    }

    private static HttpClient CreateAuthenticatedClient(ThreadsApiFactory factory, Threads.Domain.Entities.User user)
    {
        var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenService.GenerateAccessToken(user));
        return client;
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetail,
        string expectedInstance)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(expectedInstance, problem.GetProperty("instance").GetString());
        Assert.False(problem.TryGetProperty("message", out _));
    }
}
