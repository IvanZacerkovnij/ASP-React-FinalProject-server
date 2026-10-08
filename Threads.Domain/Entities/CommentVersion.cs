namespace Threads.Domain.Entities;

public class CommentVersion
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    public Guid CommentId { get; set; }

    public Comment Comment { get; set; } = null!;

    public int SchemaVersion { get; set; } = 1;
    
    public required string SnapshotJson { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}