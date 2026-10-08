using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Admin.Models;

public sealed record ReportGroup(
    ReportTargetType TargetType,
    Guid TargetId,
    int Count,
    DateTimeOffset LatestSignal);
