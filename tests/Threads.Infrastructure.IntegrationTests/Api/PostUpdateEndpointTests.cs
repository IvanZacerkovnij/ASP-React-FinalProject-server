using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostUpdateEndpointTests : DatabaseTestBase
{
    private const string Email = "post-update-user@example.com";
    private const string Username = "post_update_user";
    private const string Password = "Password123!";

    public PostUpdateEndpointTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task UpdatePost_WithoutMedia_ReturnsUpdatedPost()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/posts",
            new CreatePostRequest { Content = "Original content" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdPost = await createResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(createdPost);
        Assert.Empty(createdPost.Media);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/posts/{createdPost.Id}",
            new UpdatePostRequest { Content = "Updated content", MediaIds = [] });

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync());
        var updatedPost = await updateResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(updatedPost);
        Assert.Equal("Updated content", updatedPost.Content);
        Assert.Empty(updatedPost.Media);

        var createCommentResponse = await client.PostAsJsonAsync(
            "/api/comments",
            new CreateCommentRequest
            {
                PostId = createdPost.Id,
                Content = "Original comment"
            });
        Assert.Equal(HttpStatusCode.Created, createCommentResponse.StatusCode);
        var createdComment = await createCommentResponse.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(createdComment);
        Assert.Empty(createdComment.Attachments);

        var updateCommentResponse = await client.PutAsJsonAsync(
            $"/api/comments/{createdComment.Id}",
            new UpdateCommentRequest { Content = "Updated comment", MediaIds = [] });

        Assert.True(
            updateCommentResponse.StatusCode == HttpStatusCode.OK,
            await updateCommentResponse.Content.ReadAsStringAsync());
        var updatedComment = await updateCommentResponse.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(updatedComment);
        Assert.Equal("Updated comment", updatedComment.Content);
        Assert.Empty(updatedComment.Attachments);

        await using var verificationContext = Fixture.CreateContext();
        Assert.Equal(
            2,
            await verificationContext.PostVersions.CountAsync(version =>
                version.PostId == createdPost.Id));
        Assert.Equal(
            2,
            await verificationContext.CommentVersions.CountAsync(version =>
                version.CommentId == createdComment.Id));
    }

    [Fact]
    public async Task UpdatePost_WithRemoveLocation_ClearsLocation()
    {
        using var factory = new ThreadsApiFactory(Fixture);
        using var client = factory.CreateClient();
        var session = await RegisterAndVerifyAsync(factory, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/posts",
            new CreatePostRequest
            {
                Content = "Post with location",
                Location = new PostLocationRequest
                {
                    Id = "kyiv-id",
                    Name = "Kyiv",
                    Country = "Ukraine",
                    Latitude = 50.45,
                    Longitude = 30.52
                }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdPost = await createResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(createdPost);
        Assert.NotNull(createdPost.Location);

        using var updateContent = new StringContent(
            """{"removeLocation":true}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var updateResponse = await client.PutAsync(
            $"/api/posts/{createdPost.Id}",
            updateContent);

        Assert.True(
            updateResponse.IsSuccessStatusCode,
            await updateResponse.Content.ReadAsStringAsync());
        var updatedPost = await updateResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(updatedPost);
        Assert.Null(updatedPost.Location);

        var getResponse = await client.GetAsync($"/api/posts/{createdPost.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var persistedResponse = await getResponse.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(persistedResponse);
        Assert.Null(persistedResponse.Location);

        await using var verificationContext = Fixture.CreateContext();
        var persistedPost = await verificationContext.Posts
            .AsNoTracking()
            .SingleAsync(post => post.Id == createdPost.Id);
        Assert.Null(persistedPost.LocationName);
        Assert.Null(persistedPost.LocationPlaceId);
        Assert.Null(persistedPost.LocationCountry);
        Assert.Null(persistedPost.LocationLatitude);
        Assert.Null(persistedPost.LocationLongitude);
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
                Password = Password
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
}
