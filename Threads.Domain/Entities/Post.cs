using System.ComponentModel.DataAnnotations;
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

    public int ViewsCount { get; set; }

    public int RepostsCount { get; set; }

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public ICollection<Media> Media { get; set; } =
        new List<Media>();

    public ICollection<Comment> Comments { get; set; } =
        new List<Comment>();

    public ICollection<Like> Likes { get; set; } =
        new List<Like>();

    public ICollection<Repost> Reposts { get; set; } =
        new List<Repost>();
    
    public ICollection<Bookmark> Bookmarks { get; set; } =
        new List<Bookmark>();

    public ICollection<PostView> Views { get; set; } =
        new List<PostView>();

    public Poll? Poll { get; set; }
}
