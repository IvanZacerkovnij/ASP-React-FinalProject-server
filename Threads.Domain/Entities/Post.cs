using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Post : BaseEntity
{
    public string? Content { get; set; }

    public string? LocationName { get; set; }

    public string? LocationPlaceId { get; set; }

    public string? LocationCountry { get; set; }

    public double? LocationLatitude { get; set; }

    public double? LocationLongitude { get; set; }

    public string? EmbedUrl { get; set; }

    public string? EmbedTitle { get; set; }

    public string? EmbedDescription { get; set; }

    public string? EmbedThumbnailUrl { get; set; }

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public ICollection<Media> Media { get; set; } =
        new List<Media>();

    public ICollection<Comment> Comments { get; set; } =
        new List<Comment>();

    public ICollection<PostLike> PostLikes { get; set; } =
        new List<PostLike>();

    public ICollection<PostRepost> PostReposts { get; set; } =
        new List<PostRepost>();
    
    public ICollection<PostBookmark> PostBookmarks { get; set; } =
        new List<PostBookmark>();

    public ICollection<PostView> PostViews { get; set; } =
        new List<PostView>();

    public Poll? Poll { get; set; }
}
