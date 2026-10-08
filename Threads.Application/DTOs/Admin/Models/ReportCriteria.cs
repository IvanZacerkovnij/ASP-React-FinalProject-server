using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Admin.Models;

public sealed class ReportCriteria
{
    public int Page { get; init; }
    public string? Search { get; init; }
    public ReportTargetType? Type { get; init; }
    public ReportSource? Source { get; init; }
    public ReportStatus? Status { get; init; }
    public ReportDecision? Decision { get; init; }
    public bool Descending { get; init; }
}
