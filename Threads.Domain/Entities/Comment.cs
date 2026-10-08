using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Comment : BaseEntity
{
    public DateTimeOffset? DeletedAt { get; set; }

    public string Content { get; set; } = null!;

    public string? LinkPreviewUrl { get; set; }

    public string? LinkPreviewTitle { get; set; }

    public string? LinkPreviewImageUrl { get; set; }

    public string? LocationName { get; set; }

    public string? LocationPlaceId { get; set; }

    public string? LocationCountry { get; set; }

    public double? LocationLatitude { get; set; }

    public double? LocationLongitude { get; set; }
    
    public Guid CurrentVersionId { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public Guid? ParentCommentId { get; set; }

    public Comment? ParentComment { get; set; }
    
    public ICollection<CommentVersion> Versions { get; set; } =
        new List<CommentVersion>();

    public ICollection<Comment> Replies { get; set; } =
        new List<Comment>();

    public ICollection<CommentLike> CommentLikes { get; set; } =
        new List<CommentLike>();

    public ICollection<CommentBookmark> CommentBookmarks { get; set; } =
        new List<CommentBookmark>();

    public ICollection<CommentRepost> CommentReposts { get; set; } =
        new List<CommentRepost>();

    public ICollection<CommentView> CommentViews { get; set; } =
        new List<CommentView>();

    public ICollection<Media> Media { get; set; } =
        new List<Media>();

    public Poll? Poll { get; set; }
}
