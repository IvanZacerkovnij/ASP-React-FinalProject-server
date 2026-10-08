using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Helpers;

public static class TestEntityFactory
{
    public static User CreateUser(
        string username = "testuser",
        string email = "user@example.com",
        DateTimeOffset? createdAt = null,
        Guid? id = null,
        bool? isActive = null,
        bool? isVerified = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Username = username,
            Email = email,
            Bio = "Test Bio",
            PasswordHash = "stored-password-hash",
            IsVerified = isVerified ?? true,
            IsActive = isActive ?? true,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    public static RefreshToken CreateRefreshToken(bool? isUserActive = null, bool? isUserVerified = null)
    {
        var user = CreateUser(
            isActive: isUserActive,
            isVerified: isUserVerified);
        
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "stored-token-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
    }
    
    public static PendingRegistration CreatePendingRegistration(
        string username = "username",
        string email = "user@example.com",
        string code = "123456",
        int? timeOffset = null)
    {
        return new PendingRegistration
        {
            Email = email,
            Username = username,
            PasswordHash = "password-hash",
            DisplayName = "Display Name",
            VerificationCode = code,
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(timeOffset ?? 5)
        };
    }

    public static ScheduledPost CreateScheduledPost(
        Guid authorId,
        string? content = "scheduled post",
        DateTimeOffset? scheduledAt = null,
        Guid? id = null,
        string? linkPreviewUrl = null,
        IReadOnlyCollection<Media>? media = null)
    {
        return new ScheduledPost
        {
            Id = id ?? Guid.NewGuid(),
            AuthorId = authorId,
            Content = content,
            LinkPreviewUrl = linkPreviewUrl,
            ScheduledAt = scheduledAt ?? DateTimeOffset.UtcNow.AddHours(1),
            Media = media?.ToList() ?? []
        };
    }

    public static Media CreateMedia(
        Guid uploaderId,
        Guid? id = null,
        Guid? postId = null,
        Guid? commentId = null,
        Guid? scheduledPostId = null,
        int sortOrder = 0)
    {
        return new Media
        {
            Id = id ?? Guid.NewGuid(),
            UploadedByUserId = uploaderId,
            StorageKey = $"media/{Guid.NewGuid():N}.jpg",
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 100,
            PostId = postId,
            CommentId = commentId,
            ScheduledPostId = scheduledPostId,
            SortOrder = sortOrder
        };
    }
}
