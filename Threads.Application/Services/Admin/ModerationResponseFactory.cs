using Threads.Application.DTOs.Admin;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Admin;

public sealed class ModerationResponseFactory
{
    private readonly IUserRepository _userRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly UserResponseFactory _userResponseFactory;
    private readonly PostResponseFactory _postResponseFactory;
    private readonly CommentResponseFactory _commentResponseFactory;

    public ModerationResponseFactory(
        IUserRepository userRepository,
        IPostRepository postRepository,
        ICommentRepository commentRepository,
        UserResponseFactory userResponseFactory,
        PostResponseFactory postResponseFactory,
        CommentResponseFactory commentResponseFactory)
    {
        _userRepository = userRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _userResponseFactory = userResponseFactory;
        _postResponseFactory = postResponseFactory;
        _commentResponseFactory = commentResponseFactory;
    }

    public async Task<IReadOnlyCollection<ReportResponse>> CreateReportsAsync(
        IReadOnlyCollection<Report> reports,
        CancellationToken cancellationToken)
    {
        var targets = await CreateTargetsAsync(
            reports.Select(report => (report.TargetType, report.TargetId)),
            cancellationToken);

        return reports.Select(report => new ReportResponse
        {
            Id = report.Id,
            TargetType = ReportTargetTypeContract.Serialize(report.TargetType),
            TargetId = report.TargetId,
            Target = targets.GetValueOrDefault((report.TargetType, report.TargetId)),
            Source = Serialize(report.Source),
            Status = Serialize(report.Status),
            Decision = report.Decision.HasValue ? Serialize(report.Decision.Value) : null,
            Reason = report.Reason,
            CreatedAt = report.CreatedAt,
            Reporter = CreateUserSummary(report.Reporter),
            System = CreateSystemSignal(report),
            ResolvedAt = report.ResolvedAt,
            ResolvedBy = CreateUserSummary(report.ResolvedBy)
        }).ToArray();
    }

    public async Task<Dictionary<(ReportTargetType, Guid), ReportTargetResponse>> CreateTargetsAsync(
        IEnumerable<(ReportTargetType Type, Guid Id)> targetKeys,
        CancellationToken cancellationToken)
    {
        var keys = targetKeys.Distinct().ToArray();
        var result = new Dictionary<(ReportTargetType, Guid), ReportTargetResponse>();
        var postIds = keys.Where(key => key.Type == ReportTargetType.Posts).Select(key => key.Id).ToArray();
        var commentIds = keys.Where(key => key.Type == ReportTargetType.Comments).Select(key => key.Id).ToArray();
        var userIds = keys.Where(key => key.Type == ReportTargetType.Users).Select(key => key.Id).ToArray();

        var posts = await _postRepository.GetSummariesByIdsAsync(postIds, cancellationToken: cancellationToken);
        var comments = await _commentRepository.GetSummariesByIdsAsync(commentIds, cancellationToken: cancellationToken);
        var users = await _userRepository.GetProfilesByIdsAsync(userIds, cancellationToken);

        foreach (var post in posts)
        {
            result[(ReportTargetType.Posts, post.Id)] = new ReportTargetResponse
            {
                Type = ReportTargetTypeContract.Posts,
                Data = _postResponseFactory.Create(post)
            };
        }

        foreach (var comment in comments)
        {
            result[(ReportTargetType.Comments, comment.Id)] = new ReportTargetResponse
            {
                Type = ReportTargetTypeContract.Comments,
                Data = _commentResponseFactory.Create(comment)
            };
        }

        foreach (var user in users)
        {
            result[(ReportTargetType.Users, user.Id)] = new ReportTargetResponse
            {
                Type = ReportTargetTypeContract.Users,
                Data = _userResponseFactory.CreatePublic(user)
            };
        }

        return result;
    }

    private static ModerationUserSummary? CreateUserSummary(User? user) => user is null
        ? null
        : new ModerationUserSummary { Id = user.Id, Username = user.Username };

    private static SystemSignalResponse? CreateSystemSignal(Report report) =>
        report.Source == ReportSource.System && report.SystemCode is not null && report.SystemLabel is not null
            ? new SystemSignalResponse { Code = report.SystemCode, Label = report.SystemLabel }
            : null;

    private static string Serialize<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();
}
