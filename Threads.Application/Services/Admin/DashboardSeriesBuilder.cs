using Threads.Application.DTOs.Admin;
using Threads.Application.DTOs.Admin.Models;

namespace Threads.Application.Services.Admin;

internal static class DashboardSeriesBuilder
{
    public static IReadOnlyCollection<DashboardPointResponse> BuildAudience(
        DateTimeOffset start,
        int days,
        long baseline,
        IReadOnlyCollection<DailyCount> counts)
    {
        var byDate = counts.ToDictionary(item => item.Date, item => item.Count);
        var total = baseline;
        var points = new List<DashboardPointResponse>(days);

        for (var offset = 0; offset < days; offset++)
        {
            var date = DateOnly.FromDateTime(start.AddDays(offset).UtcDateTime);
            total += byDate.GetValueOrDefault(date);
            points.Add(CreatePoint(date, total));
        }

        return points;
    }

    public static IReadOnlyCollection<DashboardPointResponse> BuildDaily(
        DateTimeOffset start,
        int days,
        IReadOnlyCollection<DailyCount> counts)
    {
        var byDate = counts.ToDictionary(item => item.Date, item => item.Count);
        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var date = DateOnly.FromDateTime(start.AddDays(offset).UtcDateTime);
                return CreatePoint(date, byDate.GetValueOrDefault(date));
            })
            .ToArray();
    }

    private static DashboardPointResponse CreatePoint(DateOnly date, long value) =>
        new() { Date = date.ToString("yyyy-MM-dd"), Value = value };
}
