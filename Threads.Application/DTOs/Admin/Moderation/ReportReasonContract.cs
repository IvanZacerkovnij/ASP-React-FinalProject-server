namespace Threads.Application.DTOs.Admin;

public static class ReportReasonContract
{
    public const string Spam = "spam";
    public const string Harassment = "harassment";
    public const string Misinformation = "misinformation";
    public const string Violence = "violence";
    public const string Hate = "hate";
    public const string Other = "other";

    public static IReadOnlySet<string> Allowed { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Spam,
        Harassment,
        Misinformation,
        Violence,
        Hate,
        Other
    };
}
