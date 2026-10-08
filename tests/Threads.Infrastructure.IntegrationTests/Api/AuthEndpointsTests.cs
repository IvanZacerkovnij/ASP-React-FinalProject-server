using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthEndpointsTests : DatabaseTestBase
{
    private const string Email = "api-user@example.com";
    private const string Username = "api_user";
    private const string Password = "Password123!";

    public AuthEndpointsTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task RegisterVerifyLoginRefreshLogout_CompletesLifecycleAndPersistsExpectedState()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var registerResponse = await RegisterAsync(client);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var verificationCode = factory.EmailService.GetVerificationCode(Email);
        var invalidCode = verificationCode == "000000" ? "999999" : "000000";
        var invalidVerification = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest { Email = Email, Code = invalidCode });
        await AssertProblemDetailsAsync(
            invalidVerification,
            HttpStatusCode.BadRequest,
            "Invalid request",
            "Verification code is invalid or expired.",
            "/api/auth/verify-email");

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest { Email = Email, Code = verificationCode });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifiedSession = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(verifiedSession);

        var invalidLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { EmailOrUsername = Email, Password = "WrongPassword123!" });
        await AssertProblemDetailsAsync(
            invalidLogin,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Invalid credentials.",
            "/api/auth/login");

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { EmailOrUsername = Email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginSession = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginSession);
        Assert.Equal(verifiedSession.UserId, loginSession.UserId);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = verifiedSession.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedSession = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedSession);
        Assert.NotEqual(verifiedSession.RefreshToken, refreshedSession.RefreshToken);

        var logoutResponse = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest { RefreshToken = refreshedSession.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var reuseAfterLogout = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest { RefreshToken = refreshedSession.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAfterLogout.StatusCode);

        await using var dbContext = Fixture.CreateContext();
        var user = await dbContext.Users.AsNoTracking().SingleAsync();
        var tokens = await dbContext.RefreshTokens.AsNoTracking().ToListAsync();
        Assert.Equal(verifiedSession.UserId, user.Id);
        Assert.True(user.IsVerified);
        Assert.Empty(await dbContext.PendingRegistrations.AsNoTracking().ToListAsync());
        Assert.Equal(3, tokens.Count);
        Assert.NotNull(tokens.Single(token => token.TokenHash == Hash(verifiedSession.RefreshToken)).RevokedAt);
        Assert.Null(tokens.Single(token => token.TokenHash == Hash(loginSession.RefreshToken)).RevokedAt);
        Assert.NotNull(tokens.Single(token => token.TokenHash == Hash(refreshedSession.RefreshToken)).RevokedAt);
    }

    [Fact]
    public async Task Endpoints_ReturnExpectedValidationAuthorizationNotFoundAndConflictStatuses()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();

        var invalidDto = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { Username = "invalid user", Email = "not-an-email", Password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDto.StatusCode);

        var unauthenticated = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var missing = await client.GetAsync($"/api/users/by-id/{Guid.NewGuid()}");
        await AssertProblemDetailsAsync(
            missing,
            HttpStatusCode.NotFound,
            "Resource not found",
            "User was not found.",
            missing.RequestMessage!.RequestUri!.AbsolutePath);

        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client)).StatusCode);
        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = Email,
                Code = factory.EmailService.GetVerificationCode(Email)
            });
        var session = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(session);

        var duplicateRegistration = await RegisterAsync(client);
        await AssertProblemDetailsAsync(
            duplicateRegistration,
            HttpStatusCode.Conflict,
            "Conflict",
            "User with this email already exists.",
            "/api/auth/register");

        var otherAuthor = TestEntityFactory.CreateUser("other-author");
        var otherPost = TestEntityFactory.CreatePost(otherAuthor);
        await using (var dbContext = Fixture.CreateContext())
        {
            dbContext.AddRange(otherAuthor, otherPost);
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);
        var forbidden = await client.PutAsJsonAsync(
            $"/api/posts/{otherPost.Id}",
            new { Content = "Unauthorized edit" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client)
    {
        return client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = Username,
                Email = Email,
                Password = Password,
                DisplayName = "API User"
            });
    }

    private static string Hash(string value)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
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

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var problem = document.RootElement;

        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(expectedDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(expectedInstance, problem.GetProperty("instance").GetString());
        Assert.False(problem.TryGetProperty("message", out _));
    }
}
