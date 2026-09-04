using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Comment : BaseEntity
{
    public string Content { get; set; } = null!;

    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public Guid? ParentCommentId { get; set; }

    public Comment? ParentComment { get; set; }

    public ICollection<Comment> Replies { get; set; } =
        new List<Comment>();

    public ICollection<Like> Likes { get; set; } =
        new List<Like>();

    public ICollection<Bookmark> Bookmarks { get; set; } =
        new List<Bookmark>();

    public ICollection<Repost> Reposts { get; set; } =
        new List<Repost>();

    public ICollection<View> Views { get; set; } =
        new List<View>();
}
