using Threads.Application.Exceptions;

namespace Threads.Application.Services.Common;

internal static class InputNormalizer
{
    public static string NormalizeRequired(
        string? value,
        int maximumLength,
        string fieldName,
        string? requiredMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(
                requiredMessage ?? $"{fieldName} is required.");
        }

        return NormalizeLength(value.Trim(), maximumLength, fieldName);
    }

    public static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizeLength(value.Trim(), maximumLength, fieldName);
    }

    private static string NormalizeLength(
        string value,
        int maximumLength,
        string fieldName)
    {
        if (value.Length > maximumLength)
        {
            throw new RequestValidationException(
                $"{fieldName} must be {maximumLength} characters or less.");
        }

        return value;
    }
}
