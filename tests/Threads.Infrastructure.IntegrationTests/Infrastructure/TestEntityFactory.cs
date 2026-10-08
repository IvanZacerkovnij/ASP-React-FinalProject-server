using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Infrastructure.IntegrationTests.Infrastructure;

internal static class TestEntityFactory
{
    public static User CreateUser(
        string username = "testuser",
        string? email = null,
        DateTimeOffset? createdAt = null,
        Guid? id = null,
        string? bio = "Test Bio",
        string? displayName = null,
        double? locationLatitude = null,
        double? locationLongitude = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Username = username,
            Email = email ?? $"{username}@example.com",
            Bio = bio,
            DisplayName = displayName,
            LocationLatitude = locationLatitude,
            LocationLongitude = locationLongitude,
            PasswordHash = "stored-password-hash",
            IsVerified = true,
            IsActive = true,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static User CreateAdmin(
        string username = "admin",
        string? email = null,
        DateTimeOffset? createdAt = null,
        Guid? id = null)
    {
        var user = CreateUser(username, email, createdAt, id);
        user.Role = UserRole.Admin;
        return user;
    }

    public static Post CreatePost(
        User author,
        string content = "Test post",
        DateTimeOffset? createdAt = null,
        Guid? id = null,
        double? locationLatitude = null,
        double? locationLongitude = null)
    {
        return new Post
        {
            Id = id ?? Guid.NewGuid(),
            AuthorId = author.Id,
            Author = author,
            Content = content,
            LocationLatitude = locationLatitude,
            LocationLongitude = locationLongitude,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static Follow CreateFollow(
        User follower,
        User following,
        DateTimeOffset? createdAt = null)
    {
        return new Follow
        {
            FollowerId = follower.Id,
            Follower = follower,
            FollowingId = following.Id,
            Following = following,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static PostLike CreatePostLike(User user, Post post)
    {
        return new PostLike
        {
            UserId = user.Id,
            User = user,
            PostId = post.Id,
            Post = post,
            CreatedAt = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static PostRepost CreatePostRepost(User user, Post post)
    {
        return new PostRepost
        {
            UserId = user.Id,
            User = user,
            PostId = post.Id,
            Post = post,
            CreatedAt = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static Media CreatePostMedia(User uploader, Post post, string storageKey)
    {
        return new Media
        {
            StorageKey = storageKey,
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            Type = Threads.Domain.Enums.MediaType.Image,
            SizeInBytes = 100,
            UploadedByUserId = uploader.Id,
            UploadedByUser = uploader,
            PostId = post.Id,
            Post = post,
            CreatedAt = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static Comment CreateComment(
        User author,
        Post post,
        string content = "Test comment",
        DateTimeOffset? createdAt = null,
        Guid? id = null,
        Comment? parentComment = null)
    {
        return new Comment
        {
            Id = id ?? Guid.NewGuid(),
            AuthorId = author.Id,
            Author = author,
            PostId = post.Id,
            Post = post,
            ParentCommentId = parentComment?.Id,
            ParentComment = parentComment,
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
