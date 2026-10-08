using Threads.Domain.Entities;

namespace Threads.Application.DTOs.Admin.Models;

public sealed record ResolveReportResult(
    ResolveReportOutcome Outcome,
    Report? Report);
