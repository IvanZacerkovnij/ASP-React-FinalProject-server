using Threads.Application.DTOs.Admin;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Admin;
using Threads.Application.DTOs.Admin.Models;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Admin;

public sealed class AdministrationService : IAdministrationService
{
    private readonly IAdministrationRepository _repository;
    private readonly UserResponseFactory _userResponseFactory;
    private readonly ModerationResponseFactory _moderationResponseFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<AdministrationService> _logger;

    public AdministrationService(
        IAdministrationRepository repository,
        UserResponseFactory userResponseFactory,
        ModerationResponseFactory moderationResponseFactory,
        HybridCache cache,
        ILogger<AdministrationService> logger)
    {
        _repository = repository;
        _userResponseFactory = userResponseFactory;
        _moderationResponseFactory = moderationResponseFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task CreateReportAsync(
        ReportTargetType targetType,
        Guid targetId,
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason.Trim().ToLowerInvariant();
        if (!ReportReasonContract.Allowed.Contains(reason))
        {
            throw new RequestValidationException("Unknown report reason.");
        }

        var target = await _repository.GetTargetInfoAsync(targetType, targetId, cancellationToken);
        if (!target.Exists)
        {
            throw new NotFoundException("Report target was not found.");
        }

        if (target.OwnerId == reporterId)
        {
            throw new RequestValidationException("You cannot report yourself or your own content.");
        }

        await _repository.AddReportAsync(new Report
        {
            TargetType = targetType,
            TargetId = targetId,
            Source = ReportSource.User,
            Status = ReportStatus.Pending,
            Reason = reason,
            ReporterId = reporterId,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    public async Task<AdminPageResponse<AdminUserResponse>> GetUsersAsync(
        AdminUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        AdminQueryParser.ValidatePage(query.Page);
        var status = ParseUserStatus(query.Status);
        var descending = AdminQueryParser.ParseDescending(query.Sort);
        var page = await _repository.GetUsersAsync(new AdminUserCriteria
        {
            Page = query.Page,
            Search = AdminQueryParser.NormalizeSearch(query.Search),
            IsBlocked = status,
            Descending = descending
        }, cancellationToken);

        return new AdminPageResponse<AdminUserResponse>
        {
            Items = page.Items.Select(_userResponseFactory.CreateAdmin).ToArray(),
            Pagination = MapPagination(page.Page, page.Total, page.TotalPages)
        };
    }

    public async Task<AdminUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserAsync(id, cancellationToken);
        return user is null ? null : _userResponseFactory.CreateAdmin(user);
    }

    public async Task SetUserBlockedAsync(
        Guid id,
        Guid adminId,
        bool isBlocked,
        CancellationToken cancellationToken = default)
    {
        AdminActionPolicy.EnsureNotSelf(id, adminId);
        var user = await _repository.GetUserAsync(id, cancellationToken);
        if (!await _repository.SetUserBlockedAsync(id, isBlocked, cancellationToken))
        {
            throw new NotFoundException("User was not found.");
        }
        await InvalidateUserAsync(user, cancellationToken);
    }

    public async Task DeleteUserAsync(Guid id, Guid adminId, CancellationToken cancellationToken = default)
    {
        AdminActionPolicy.EnsureNotSelf(id, adminId);
        var user = await _repository.GetUserAsync(id, cancellationToken);
        if (!await _repository.DeleteUserAsync(id, cancellationToken))
        {
            throw new NotFoundException("User was not found.");
        }
        await InvalidateUserAsync(user, cancellationToken);
    }

    public async Task<AdminPageResponse<ReportResponse>> GetReportsAsync(
        ModerationQuery query,
        CancellationToken cancellationToken = default)
    {
        AdminQueryParser.ValidatePage(query.Page);
        var page = await _repository.GetReportsAsync(new ReportCriteria
        {
            Page = query.Page,
            Search = AdminQueryParser.NormalizeSearch(query.Search),
            Type = AdminQueryParser.ParseOptionalEnum<ReportTargetType>(query.Type, "type"),
            Source = AdminQueryParser.ParseOptionalEnum<ReportSource>(query.Source, "source"),
            Status = AdminQueryParser.ParseOptionalEnum<ReportStatus>(query.Status, "status"),
            Decision = AdminQueryParser.ParseOptionalEnum<ReportDecision>(query.Decision, "decision"),
            Descending = AdminQueryParser.ParseDescending(query.Sort)
        }, cancellationToken);
        var reports = await _moderationResponseFactory.CreateReportsAsync(page.Items, cancellationToken);

        return new AdminPageResponse<ReportResponse>
        {
            Items = reports,
            Pagination = MapPagination(page.Page, page.Total, page.TotalPages)
        };
    }

    public async Task<ReportResponse?> GetReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _repository.GetReportAsync(id, cancellationToken);
        if (report is null)
        {
            return null;
        }

        return (await _moderationResponseFactory.CreateReportsAsync([report], cancellationToken)).Single();
    }

    public async Task<ReportResponse> ResolveReportAsync(
        Guid id,
        Guid adminId,
        ResolveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Status.Trim(), "resolved", StringComparison.Ordinal))
        {
            throw new RequestValidationException("Status must be resolved.");
        }

        var decision = AdminQueryParser.ParseRequiredEnum<ReportDecision>(request.Decision, "decision");
        var current = await _repository.GetReportAsync(id, cancellationToken)
            ?? throw new NotFoundException("Report was not found.");
        var affectedUser = current.TargetType == ReportTargetType.Users &&
                           decision is ReportDecision.Blocked or ReportDecision.Deleted
            ? await _repository.GetUserAsync(current.TargetId, cancellationToken)
            : null;

        if (decision == ReportDecision.Blocked && current.TargetType != ReportTargetType.Users)
        {
            throw new RequestValidationException("Blocked decision is only valid for user targets.");
        }

        var result = await _repository.ResolveReportAsync(id, adminId, decision, cancellationToken);
        if (result.Outcome == ResolveReportOutcome.Conflict)
        {
            throw new ConflictException("Report was already resolved with another decision.");
        }
        if (result.Outcome == ResolveReportOutcome.TargetUnavailable)
        {
            throw new ConflictException("Report target is not available for this decision.");
        }
        if (result.Outcome == ResolveReportOutcome.NotFound || result.Report is null)
        {
            throw new NotFoundException("Report was not found.");
        }

        await InvalidateUserAsync(affectedUser, cancellationToken);

        return (await _moderationResponseFactory.CreateReportsAsync([result.Report], cancellationToken)).Single();
    }

    public async Task<IReadOnlyCollection<DashboardMetricResponse>> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await _repository.GetDashboardCountsAsync(cancellationToken);
        return
        [
            new() { Id = DashboardMetricContract.TotalUsersId, Title = DashboardMetricContract.TotalUsersTitle, Value = counts.Users },
            new() { Id = DashboardMetricContract.ActiveUsersId, Title = DashboardMetricContract.ActiveUsersTitle, Value = counts.ActiveUsers },
            new() { Id = DashboardMetricContract.BlockedUsersId, Title = DashboardMetricContract.BlockedUsersTitle, Value = counts.BlockedUsers },
            new() { Id = DashboardMetricContract.PendingReportsId, Title = DashboardMetricContract.PendingReportsTitle, Value = counts.PendingReports }
        ];
    }

    public async Task<DashboardChartsResponse> GetChartsAsync(
        string period,
        CancellationToken cancellationToken = default)
    {
        if (!DashboardPeriodContract.DaysByPeriod.ContainsKey(period))
        {
            throw new RequestValidationException("Period must be 7d or 30d.");
        }

        var audience = new Dictionary<string, IReadOnlyCollection<DashboardPointResponse>>();
        var activity = new Dictionary<string, DashboardActivityResponse>();

        foreach (var (key, days) in DashboardPeriodContract.DaysByPeriod)
        {
            var end = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(1);
            var start = end.AddDays(-days);
            var baseline = await _repository.GetUsersCreatedBeforeAsync(start, cancellationToken);
            var users = await _repository.GetUsersCreatedByDayAsync(start, end, cancellationToken);
            var posts = await _repository.GetPostsCreatedByDayAsync(start, end, cancellationToken);
            var comments = await _repository.GetCommentsCreatedByDayAsync(start, end, cancellationToken);
            audience[key] = DashboardSeriesBuilder.BuildAudience(start, days, baseline, users);
            activity[key] = new DashboardActivityResponse
            {
                Posts = DashboardSeriesBuilder.BuildDaily(start, days, posts),
                Comments = DashboardSeriesBuilder.BuildDaily(start, days, comments)
            };
        }

        return new DashboardChartsResponse { Audience = audience, Activity = activity };
    }

    public async Task<DashboardTablesResponse> GetTablesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > AdminPaginationContract.MaximumTableLimit)
        {
            throw new RequestValidationException("Limit must be between 1 and 100.");
        }

        var users = await _repository.GetLatestUsersAsync(limit, cancellationToken);
        var groups = await _repository.GetLatestReportGroupsAsync(limit, cancellationToken);
        var targets = await _moderationResponseFactory.CreateTargetsAsync(
            groups.Select(group => (group.TargetType, group.TargetId)),
            cancellationToken);

        return new DashboardTablesResponse
        {
            LatestUsers = users.Select(_userResponseFactory.CreateAdmin).ToArray(),
            LatestReports = groups.Select(group => new ReportRowResponse
            {
                TargetType = ReportTargetTypeContract.Serialize(group.TargetType),
                TargetId = group.TargetId,
                Count = group.Count,
                LatestSignal = group.LatestSignal,
                Target = targets.GetValueOrDefault((group.TargetType, group.TargetId))
            }).ToArray()
        };
    }

    private static PaginationResponse MapPagination(int page, int total, int totalPages) =>
        new() { Page = page, Total = total, TotalPages = totalPages };

    private static bool? ParseUserStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        AdminUserStatusContract.Active => false,
        AdminUserStatusContract.Blocked => true,
        _ => throw new RequestValidationException("Status must be active or blocked.")
    };

    private Task InvalidateUserAsync(User? user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return Task.CompletedTask;
        }

        return CacheInvalidation.TryRemoveAsync(
            _cache,
            _logger,
            UserProfileCache.GetProfileKey(user.Id),
            UserProfileCache.GetUsernameKey(user.Username.ToLowerInvariant()));
    }
}
