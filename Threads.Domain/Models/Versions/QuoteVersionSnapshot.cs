using Threads.Domain.Enums;

namespace Threads.Domain.Models.Versions;

public sealed class QuoteVersionSnapshot
{
    public ContentTargetType TargetType { get; init; }
    
    public Guid TargetId { get; init; }
    
    public Guid TargetVersionId { get; init; }
}