using Threads.Application.Exceptions;

namespace Threads.Application.Services.Common;

internal static class SearchQueryNormalizer
{
    public static string? Normalize(
        string? query,
        int maximumLength,
        string queryName)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var normalizedQuery = query.Trim();

        if (normalizedQuery.Length > maximumLength)
        {
            throw new RequestValidationException(
                $"{queryName} must contain at most {maximumLength} characters.");
        }

        return normalizedQuery;
    }
}
