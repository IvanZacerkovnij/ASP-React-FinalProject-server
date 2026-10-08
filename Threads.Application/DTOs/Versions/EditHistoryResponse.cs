using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Versions;

public sealed class EditHistoryResponse<T>
{
    public ContentTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    public IReadOnlyCollection<T> Versions { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }
}
