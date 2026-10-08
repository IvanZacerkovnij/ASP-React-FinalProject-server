using Threads.Application.DTOs.Admin;
using Threads.Application.Exceptions;

namespace Threads.Application.Services.Admin;

internal static class AdminQueryParser
{
    public static void ValidatePage(int page)
    {
        if (page < AdminPaginationContract.DefaultPage)
        {
            throw new RequestValidationException("Page must be a positive integer.");
        }
    }

    public static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

    public static bool ParseDescending(string? sort) => (sort?.Trim().ToLowerInvariant()) switch
    {
        null or "" or AdminSortContract.Newest => true,
        AdminSortContract.Oldest => false,
        _ => throw new RequestValidationException("Sort must be newest or oldest.")
    };

    public static TEnum? ParseOptionalEnum<TEnum>(string? value, string name)
        where TEnum : struct, Enum
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ParseRequiredEnum<TEnum>(value, name);
    }

    public static TEnum ParseRequiredEnum<TEnum>(string value, string name)
        where TEnum : struct, Enum
    {
        var normalized = value.Trim();
        if (!Enum.TryParse<TEnum>(normalized, true, out var parsed) ||
            !string.Equals(parsed.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"Unknown {name} value.");
        }

        return parsed;
    }
}
