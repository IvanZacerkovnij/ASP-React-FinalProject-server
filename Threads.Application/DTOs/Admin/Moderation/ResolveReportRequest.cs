namespace Threads.Application.DTOs.Admin;

public sealed class ResolveReportRequest
{
    public string Status { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
}
