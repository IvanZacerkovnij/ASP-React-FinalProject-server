using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class ProfileEndpointsTests : DatabaseTestBase
{
    private const string Email = "profile-user@example.com";
    private const string Username = "profile_user";
    private const string Password = "Password123!";

    public ProfileEndpointsTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task UpdateMe_WithBirthDateVisibility_PersistsAndHidesBirthDateFromAnonymousViewer()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);

        var updateResponse = await client.PutAsync("/api/me", CreateProfileForm("followers", "only_me"));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadJsonAsync(updateResponse);
        Assert.Equal("2000-01-02", updated.GetProperty("birthDate").GetString());
        Assert.Equal("followers", updated.GetProperty("birthDateVisibility").GetString());
        Assert.Equal("only_me", updated.GetProperty("birthYearVisibility").GetString());

        var meResponse = await client.GetAsync("/api/me");
        var me = await ReadJsonAsync(meResponse);
        Assert.Equal("2000-01-02", me.GetProperty("birthDate").GetString());
        Assert.Equal("only_me", me.GetProperty("birthYearVisibility").GetString());

        using var anonymousClient = factory.CreateClient();
        var publicResponse = await anonymousClient.GetAsync($"/api/users/by-username/{Username}");

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicProfile = await ReadJsonAsync(publicResponse);
        Assert.Equal(JsonValueKind.Null, publicProfile.GetProperty("birthDate").ValueKind);
    }

    [Fact]
    public async Task UpdateMe_WithUnknownVisibility_ReturnsBadRequest()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);

        var response = await client.PutAsync("/api/me", CreateProfileForm("everyone", "only_me"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_WithLocationFormFields_PersistsLocation()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);
        using var form = new MultipartFormDataContent
        {
            { new StringContent("lviv-id"), "Location.Id" },
            { new StringContent("Lviv"), "Location.Name" },
            { new StringContent("Ukraine"), "Location.Country" },
            { new StringContent("49"), "Location.Latitude" },
            { new StringContent("24"), "Location.Longitude" }
        };

        var updateResponse = await client.PutAsync("/api/me", form);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await ReadJsonAsync(updateResponse);
        var location = updated.GetProperty("location");
        Assert.Equal("lviv-id", location.GetProperty("id").GetString());
        Assert.Equal("Lviv", location.GetProperty("name").GetString());
        Assert.Equal("Ukraine", location.GetProperty("country").GetString());
        Assert.Equal(49, location.GetProperty("latitude").GetDouble());
        Assert.Equal(24, location.GetProperty("longitude").GetDouble());

        var meResponse = await client.GetAsync("/api/me");
        var me = await ReadJsonAsync(meResponse);
        Assert.Equal("Lviv", me.GetProperty("location").GetProperty("name").GetString());
    }

    private static MultipartFormDataContent CreateProfileForm(
        string birthDateVisibility,
        string birthYearVisibility)
    {
        return new MultipartFormDataContent
        {
            { new StringContent("2000-01-02"), "BirthDate" },
            { new StringContent(birthDateVisibility), "BirthDateVisibility" },
            { new StringContent(birthYearVisibility), "BirthYearVisibility" }
        };
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
                DisplayName = "Profile User"
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
