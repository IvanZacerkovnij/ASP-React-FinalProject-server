namespace Threads.Application.DTOs.Admin;

public sealed class ReportRowResponse
{
    public required string TargetType { get; init; }
    public Guid TargetId { get; init; }
    public int Count { get; init; }
    public DateTimeOffset LatestSignal { get; init; }
    public ReportTargetResponse? Target { get; init; }
}
