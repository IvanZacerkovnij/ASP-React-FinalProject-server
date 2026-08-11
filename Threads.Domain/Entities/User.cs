using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PasswordResetCodeHash { get; set; }

    public DateTimeOffset? PasswordResetCodeExpiresAt { get; set; }

    public string? PendingPasswordHash { get; set; }

    public string? DisplayName { get; set; }

    public string? Bio { get; set; }

    public string? Location { get; set; }

    public string? LocationPlaceId { get; set; }

    public string? LocationCountry { get; set; }

    public double? LocationLatitude { get; set; }

    public double? LocationLongitude { get; set; }

    public string? AvatarObjectKey { get; set; }

    public string? BannerObjectKey { get; set; }

    public bool IsVerified { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Post> Posts { get; set; } =
        new List<Post>();

    public ICollection<Comment> Comments { get; set; } =
        new List<Comment>();

    public ICollection<Like> Likes { get; set; } =
        new List<Like>();

    public ICollection<Repost> Reposts { get; set; } =
        new List<Repost>();
    
    public ICollection<Bookmark> Bookmarks { get; set; } =
        new List<Bookmark>();

    public ICollection<PostView> PostViews { get; set; } =
        new List<PostView>();

    public ICollection<PollVote> PollVotes { get; set; } =
        new List<PollVote>();

    public ICollection<Follow> FollowingRelations { get; set; } =
        new List<Follow>();

    public ICollection<Follow> FollowerRelations { get; set; } =
        new List<Follow>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } =
        new List<RefreshToken>();

    public ICollection<Media> UploadedMedia { get; set; } =
        new List<Media>();
}
