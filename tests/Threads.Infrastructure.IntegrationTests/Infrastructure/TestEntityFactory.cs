using Threads.Domain.Entities;

namespace Threads.Infrastructure.IntegrationTests.Infrastructure;

internal static class TestEntityFactory
{
    public static User CreateUser(
        string username = "testuser",
        string? email = null,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Username = username,
            Email = email ?? $"{username}@example.com",
            PasswordHash = "stored-password-hash",
            IsVerified = true,
            IsActive = true,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static Post CreatePost(
        User author,
        string content = "Test post",
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        return new Post
        {
            Id = id ?? Guid.NewGuid(),
            AuthorId = author.Id,
            Author = author,
            Content = content,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static Comment CreateComment(
        User author,
        Post post,
        string content = "Test comment",
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        return new Comment
        {
            Id = id ?? Guid.NewGuid(),
            AuthorId = author.Id,
            Author = author,
            PostId = post.Id,
            Post = post,
            Content = content,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static RefreshToken CreateRefreshToken(
        User user,
        string tokenHash,
        DateTimeOffset? expiresAt = null,
        Guid? id = null)
    {
        return new RefreshToken
        {
            Id = id ?? Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = new DateTimeOffset(2026, 1, 4, 0, 0, 0, TimeSpan.Zero)
        };
    }
}
