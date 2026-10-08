namespace Threads.Application.DTOs.Admin.Models;

public enum ResolveReportOutcome
{
    Resolved,
    Idempotent,
    Conflict,
    NotFound,
    TargetUnavailable
}
