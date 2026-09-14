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

    public ICollection<CommentLike> CommentLikes { get; set; } =
        new List<CommentLike>();

    public ICollection<CommentBookmark> CommentBookmarks { get; set; } =
        new List<CommentBookmark>();

    public ICollection<CommentRepost> CommentReposts { get; set; } =
        new List<CommentRepost>();

    public ICollection<CommentView> CommentViews { get; set; } =
        new List<CommentView>();
}
