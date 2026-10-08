namespace Threads.Application.DTOs.Admin;

public sealed class ReportResponse
{
    public Guid Id { get; init; }
    public required string TargetType { get; init; }
    public Guid TargetId { get; init; }
    public ReportTargetResponse? Target { get; init; }
    public required string Source { get; init; }
    public required string Status { get; init; }
    public string? Decision { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public ModerationUserSummary? Reporter { get; init; }
    public SystemSignalResponse? System { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public ModerationUserSummary? ResolvedBy { get; init; }
}
