using System.Security.Cryptography;
using Threads.Application.Interfaces.Auth;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

internal static class AuthCode
{
    private const int PasswordResetCodeLifetimeMinutes = 15;
    private const int EmailVerificationCodeLifetimeMinutes = 15;

    public static string Generate()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");
    }

    public static void SetPasswordResetCode(
        User user,
        string code,
        IAuthCodeHasher authCodeHasher)
    {
        var normalizedCode = AuthInputNormalizer.NormalizeSixDigitCode(code, "Reset code");
        user.PasswordResetCodeHash = authCodeHasher.Hash(
            normalizedCode,
            GetPasswordResetContext(user.Id));
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(
            PasswordResetCodeLifetimeMinutes);
    }

    public static void SetPasswordChangeCode(
        User user,
        string code,
        IAuthCodeHasher authCodeHasher)
    {
        var normalizedCode = AuthInputNormalizer.NormalizeSixDigitCode(code, "Confirmation code");
        user.PasswordResetCodeHash = authCodeHasher.Hash(
            normalizedCode,
            GetPasswordChangeContext(user.Id));
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(
            PasswordResetCodeLifetimeMinutes);
    }

    public static void ClearPasswordResetCode(User user)
    {
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresAt = null;
    }

    public static void SetEmailVerificationCode(
        PendingRegistration pendingRegistration,
        string code)
    {
        pendingRegistration.VerificationCode = AuthInputNormalizer.NormalizeSixDigitCode(
            code,
            "Verification code");
        pendingRegistration.VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(
            EmailVerificationCodeLifetimeMinutes);
    }

    public static bool IsPasswordResetCodeValid(
        User? user,
        string code,
        IAuthCodeHasher authCodeHasher)
    {
        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(user.PasswordResetCodeHash) ||
            user.PasswordResetCodeExpiresAt is null ||
            user.PasswordResetCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var normalizedCode = AuthInputNormalizer.NormalizeSixDigitCode(code, "Reset code");
        return authCodeHasher.Verify(
            normalizedCode,
            GetPasswordResetContext(user.Id),
            user.PasswordResetCodeHash);
    }

    public static bool IsPasswordChangeCodeValid(
        User? user,
        string code,
        IAuthCodeHasher authCodeHasher)
    {
        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(user.PasswordResetCodeHash) ||
            user.PasswordResetCodeExpiresAt is null ||
            user.PasswordResetCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var normalizedCode = AuthInputNormalizer.NormalizeSixDigitCode(code, "Confirmation code");
        return authCodeHasher.Verify(
            normalizedCode,
            GetPasswordChangeContext(user.Id),
            user.PasswordResetCodeHash);
    }

    public static bool IsEmailVerificationCodeValid(
        PendingRegistration? pendingRegistration,
        string code)
    {
        if (pendingRegistration is null ||
            pendingRegistration.VerificationCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var normalizedCode = AuthInputNormalizer.NormalizeSixDigitCode(code, "Verification code");
        return pendingRegistration.VerificationCode == normalizedCode;
    }

    private static string GetPasswordResetContext(Guid userId)
    {
        return $"password-reset:{userId:N}";
    }

    private static string GetPasswordChangeContext(Guid userId)
    {
        return $"password-change:{userId:N}";
    }
}
