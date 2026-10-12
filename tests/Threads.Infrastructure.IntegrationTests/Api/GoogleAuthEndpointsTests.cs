using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Interfaces.Security;
using Threads.Infrastructure.IntegrationTests.Infrastructure;
using Threads.Infrastructure.IntegrationTests.Security;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class GoogleAuthEndpointsTests(PostgreSqlFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task GoogleLogin_CreatesUserReusesSubjectAndSupportsRefresh()
    {
        using var issuer = new GoogleTokenTestIssuer();
        using var baseFactory = new ThreadsApiFactory(Fixture);
        using var factory = ConfigureGoogle(baseFactory, issuer);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/api/auth/google", Request(issuer.CreateToken()));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstSession = await firstResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(firstSession);

        var secondResponse = await client.PostAsJsonAsync("/api/auth/google",
            Request(issuer.CreateToken(email: "changed@gmail.com")));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondSession = await secondResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(secondSession);
        Assert.Equal(firstSession.UserId, secondSession.UserId);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = firstSession.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var passwordLogin = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { EmailOrUsername = "googleuser@gmail.com", Password = "Password123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, passwordLogin.StatusCode);

        await using var context = Fixture.CreateContext();
        var user = await context.Users.SingleAsync();
        Assert.Equal(firstSession.UserId, user.Id);
        Assert.Equal("google-user-123", user.GoogleSubject);
        Assert.Equal("googleuser@gmail.com", user.Email);
        Assert.Null(user.PasswordHash);
        Assert.True(user.IsVerified);
        Assert.Equal(3, await context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task GoogleLink_RequiresLocalSessionAndAllowsExistingAccountToLogin()
    {
        var user = TestEntityFactory.CreateUser("existing", "googleuser@gmail.com");
        user.IsVerified = true;
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        using var issuer = new GoogleTokenTestIssuer();
        using var baseFactory = new ThreadsApiFactory(Fixture);
        using var factory = ConfigureGoogle(baseFactory, issuer);
        using var client = factory.CreateClient();
        var request = Request(issuer.CreateToken());

        var conflict = await client.PostAsJsonAsync("/api/auth/google", request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);

        var anonymousLink = await client.PostAsJsonAsync("/api/auth/google/link", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLink.StatusCode);
        using var scope = factory.Services.CreateScope();
        var accessToken = scope.ServiceProvider.GetRequiredService<ITokenService>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.GenerateAccessToken(user));

        var linked = await client.PostAsJsonAsync("/api/auth/google/link", request);
        Assert.Equal(HttpStatusCode.NoContent, linked.StatusCode);
        var replacement = await client.PostAsJsonAsync("/api/auth/google/link", Request(issuer.CreateToken(subject: "other-subject")));
        Assert.Equal(HttpStatusCode.Conflict, replacement.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/google", request);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(user.Id, session?.UserId);
        await using var verificationContext = Fixture.CreateContext();
        Assert.NotNull((await verificationContext.Users.SingleAsync()).PasswordHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GoogleLogin_InactiveOrDeletedLinkedAccount_DoesNotIssueSession(bool deleted)
    {
        var user = TestEntityFactory.CreateUser();
        user.GoogleSubject = "google-user-123";
        user.IsActive = deleted;
        user.DeletedAt = deleted ? DateTimeOffset.UtcNow : null;
        await using (var context = Fixture.CreateContext())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        using var issuer = new GoogleTokenTestIssuer();
        using var baseFactory = new ThreadsApiFactory(Fixture);
        using var factory = ConfigureGoogle(baseFactory, issuer);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/google", Request(issuer.CreateToken()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(1, await verificationContext.Users.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await verificationContext.RefreshTokens.ToListAsync());
    }

    [Theory]
    [InlineData("invalid", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("external-email", HttpStatusCode.BadRequest)]
    [InlineData("empty", HttpStatusCode.BadRequest)]
    public async Task GoogleLogin_InvalidRequest_ReturnsProblemDetailsAndDoesNotCreateUser(
        string failure, HttpStatusCode expectedStatus)
    {
        using var issuer = new GoogleTokenTestIssuer();
        using var baseFactory = new ThreadsApiFactory(Fixture);
        using var factory = ConfigureGoogle(baseFactory, issuer);
        using var client = factory.CreateClient();
        var token = failure switch
        {
            "expired" => issuer.CreateToken(expired: true),
            "audience" => issuer.CreateToken(audience: "other-client"),
            "external-email" => issuer.CreateToken(email: "user@example.com"),
            "empty" => "",
            _ => "invalid-token"
        };

        var response = await client.PostAsJsonAsync("/api/auth/google", Request(token));

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var context = Fixture.CreateContext();
        Assert.Empty(await context.Users.ToListAsync());
        Assert.Empty(await context.RefreshTokens.ToListAsync());
    }

    private static GoogleLoginRequest Request(string token) => new() { IdToken = token };

    private static WebApplicationFactory<Threads.Api.Program> ConfigureGoogle(
        ThreadsApiFactory factory, GoogleTokenTestIssuer issuer) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Google:ClientId"] = GoogleTokenTestIssuer.ClientId }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConfigurationManager<OpenIdConnectConfiguration>>();
            services.AddSingleton(issuer.ConfigurationManager);
        });
    });
}
