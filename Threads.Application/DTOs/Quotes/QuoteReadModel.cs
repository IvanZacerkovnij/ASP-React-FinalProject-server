using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Quotes;

public sealed class QuoteReadModel
{
    public ContentTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    public Guid TargetVersionId { get; init; }
}
