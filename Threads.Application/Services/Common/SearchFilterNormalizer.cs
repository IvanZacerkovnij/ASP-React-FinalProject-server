using Threads.Application.Exceptions;

namespace Threads.Application.Services.Common;

internal static class SearchFilterNormalizer
{
    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? NormalizeOption(
        string? value,
        string allowedValue,
        string fieldName)
    {
        var normalized = NormalizeText(value)?.ToLowerInvariant();
        if (normalized is not null && normalized != allowedValue)
        {
            throw new RequestValidationException(
                $"{fieldName} must be '{allowedValue}'.");
        }

        return normalized;
    }

    public static IReadOnlyCollection<string> SplitWords(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized is null
            ? []
            : normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.ToLowerInvariant())
                .Distinct()
                .ToArray();
    }
}
