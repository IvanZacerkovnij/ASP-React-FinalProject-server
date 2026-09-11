using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, StringLength(255)]
    public required string Email { get; init; }
    
    [Required, RegularExpression(@"^\d{6}$")]
    public required string Code { get; init; }
    
    [Required, StringLength(32 , MinimumLength = 8)]
    public required string NewPassword { get; init; }
}
