namespace Threads.Domain.Entities;

public class PostVersion
{
    public Guid Id { get; set; } =  Guid.NewGuid();
    
    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;

    public int SchemaVersion { get; set; } = 1;
    
    public required string SnapshotJson { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}