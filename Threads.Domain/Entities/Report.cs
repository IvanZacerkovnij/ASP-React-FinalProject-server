using Threads.Domain.Common;
using Threads.Domain.Enums;

namespace Threads.Domain.Entities;

public sealed class Report : BaseEntity
{
    public ReportTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public ReportSource Source { get; set; } = ReportSource.User;

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public ReportDecision? Decision { get; set; }

    public required string Reason { get; set; }

    public Guid? ReporterId { get; set; }

    public User? Reporter { get; set; }

    public string? SystemCode { get; set; }

    public string? SystemLabel { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedById { get; set; }

    public User? ResolvedBy { get; set; }
}
