namespace Threads.Application.DTOs.Admin;

public static class DashboardPeriodContract
{
    public const string SevenDays = "7d";
    public const string ThirtyDays = "30d";

    public static IReadOnlyDictionary<string, int> DaysByPeriod { get; } =
        new Dictionary<string, int>
        {
            [SevenDays] = 7,
            [ThirtyDays] = 30
        };
}
