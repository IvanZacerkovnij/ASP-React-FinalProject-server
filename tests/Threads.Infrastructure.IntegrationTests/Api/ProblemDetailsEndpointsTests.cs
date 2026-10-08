using System.Net;
using System.Text.Json;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ProblemDetailsEndpointsTests : DatabaseTestBase
{
    public ProblemDetailsEndpointsTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Theory]
    [InlineData("/api/unknown", HttpStatusCode.NotFound, "Not Found")]
    [InlineData("/api/auth/login", HttpStatusCode.MethodNotAllowed, "Method Not Allowed")]
    public async Task FrameworkErrors_ReturnProblemDetails(
        string path,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(path, problem.GetProperty("instance").GetString());
        Assert.False(problem.TryGetProperty("message", out _));
    }
}
