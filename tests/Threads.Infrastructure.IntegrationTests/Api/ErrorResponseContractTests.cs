using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ErrorResponseContractTests : DatabaseTestBase
{
    public ErrorResponseContractTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ModelValidationError_ReturnsRfc7807()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { Username = "invalid user", Email = "invalid", Password = "short" });

        await AssertRfc7807Async(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RateLimitRejection_ReturnsRfc7807()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { EmailOrUsername = "missing@example.com", Password = "Password123!" });
        }

        Assert.NotNull(response);
        using (response)
        {
            await AssertRfc7807Async(response, HttpStatusCode.TooManyRequests);
        }
    }

    private static async Task AssertRfc7807Async(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(problem.TryGetProperty("message", out _));
    }
}
