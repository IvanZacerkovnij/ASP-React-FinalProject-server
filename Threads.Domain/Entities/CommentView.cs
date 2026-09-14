namespace Threads.Domain.Entities;

public class CommentView
{
    public Guid CommentId { get; set; }
    public Comment Comment { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
