using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Quotes;

public sealed class QuoteResponse
{
    public ContentTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    public Guid TargetVersionId { get; init; }

    public bool HasNewVersion { get; init; }

    public IReadOnlyCollection<string> ReplyingToUsernames { get; init; } = [];

    public object? Target { get; init; }
}