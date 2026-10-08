using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Admin;

public static class ReportTargetTypeContract
{
    public const string Posts = "posts";
    public const string Comments = "comments";
    public const string Users = "users";

    public static string Serialize(ReportTargetType targetType) => targetType switch
    {
        ReportTargetType.Posts => Posts,
        ReportTargetType.Comments => Comments,
        ReportTargetType.Users => Users,
        _ => throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null)
    };
}
