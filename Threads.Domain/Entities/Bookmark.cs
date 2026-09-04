using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Bookmark : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid? PostId { get; set; }
    public Post? Post { get; set; }
    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }
}
