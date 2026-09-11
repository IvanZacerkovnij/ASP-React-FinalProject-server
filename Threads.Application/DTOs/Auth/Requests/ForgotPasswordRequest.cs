using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(255)]
    public required string Email { get; init; }
}
