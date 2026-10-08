using Threads.Domain.Enums;

namespace Threads.Domain.Entities;

public class PostQuote
{
    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;
    
    public ContentTargetType TargetType { get; set; }
    
    public Guid TargetId { get; set; }
    
    public Guid TargetVersionId { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } =  DateTimeOffset.UtcNow;
}