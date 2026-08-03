namespace Threads.Domain.Common;

public class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } =  DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}