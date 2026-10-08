using System.Data;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Admin;
using Threads.Application.DTOs.Admin.Models;
using Threads.Application.Interfaces.Admin;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Infrastructure.Data.Repositories.Admin;

public sealed partial class AdministrationRepository
{
    public async Task<ReportTargetInfo> GetTargetInfoAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        return targetType switch
        {
            ReportTargetType.Posts => await _dbContext.Posts
                .AsNoTracking()
                .Where(post => post.Id == targetId)
                .Select(post => new ReportTargetInfo(true, post.AuthorId))
                .FirstOrDefaultAsync(cancellationToken) ?? new ReportTargetInfo(false, Guid.Empty),
            ReportTargetType.Comments => await _dbContext.Comments
                .AsNoTracking()
                .Where(comment => comment.Id == targetId)
                .Select(comment => new ReportTargetInfo(true, comment.AuthorId))
                .FirstOrDefaultAsync(cancellationToken) ?? new ReportTargetInfo(false, Guid.Empty),
            ReportTargetType.Users => await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == targetId)
                .Select(user => new ReportTargetInfo(true, user.Id))
                .FirstOrDefaultAsync(cancellationToken) ?? new ReportTargetInfo(false, Guid.Empty),
            _ => new ReportTargetInfo(false, Guid.Empty)
        };
    }

    public async Task AddReportAsync(Report report, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reports.AddAsync(report, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

