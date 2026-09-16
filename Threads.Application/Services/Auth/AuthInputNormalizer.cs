using Threads.Application.Exceptions;

namespace Threads.Application.Services.Auth;

internal static class AuthInputNormalizer
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 64;

    public static string NormalizeEmail(string? email)
    {
        return NormalizeRequired(email, "Email is required.")
            .ToLowerInvariant();
    }

    public static string NormalizeUsername(string? username)
    {
        return NormalizeRequired(username, "Username is required.")
            .ToLowerInvariant();
    }

    public static string NormalizePassword(string? password, string paramName)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new RequestValidationException($"{paramName} is required.");
        }

        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            throw new RequestValidationException(
                $"{paramName} must be between {MinPasswordLength} and {MaxPasswordLength} characters.");
        }

        return password;
    }

    public static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(errorMessage);
        }

        return value.Trim();
    }

    public static string NormalizeSixDigitCode(string? code, string codeName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new RequestValidationException($"{codeName} is required.");
        }

        var normalizedCode = code.Trim();

        if (normalizedCode.Length != 6 || normalizedCode.Any(character => !char.IsDigit(character)))
        {
            throw new RequestValidationException($"{codeName} must contain exactly 6 digits.");
        }

        return normalizedCode;
    }

    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
