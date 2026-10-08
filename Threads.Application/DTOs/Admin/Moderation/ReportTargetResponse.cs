namespace Threads.Application.DTOs.Admin;

public sealed class ReportTargetResponse
{
    public required string Type { get; init; }
    public required object Data { get; init; }
}
