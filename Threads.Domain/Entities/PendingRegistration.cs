using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class PendingRegistration : BaseEntity
{
    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string VerificationCode { get; set; } = null!;

    public DateTimeOffset VerificationCodeExpiresAt { get; set; }

    public string? DisplayName { get; set; }
}
