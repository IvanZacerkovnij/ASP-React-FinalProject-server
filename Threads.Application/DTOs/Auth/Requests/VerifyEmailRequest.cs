using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public class VerifyEmailRequest
{
    [Required, EmailAddress, StringLength(255)]
    public required string Email { get; init; }
    
    [Required, RegularExpression(@"^\d{6}$")]
    public required string Code {get; init;}
}