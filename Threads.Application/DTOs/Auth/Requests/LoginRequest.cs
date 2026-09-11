using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class LoginRequest
{
    [Required, StringLength(255, MinimumLength = 1)]
    public required string EmailOrUsername { get; init; }
    
    [Required, StringLength(32, MinimumLength = 8)]
    public required string Password { get; init; }
}
