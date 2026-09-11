using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class RegisterRequest
{
    [Required, StringLength(50 , MinimumLength = 1)]
    public required string Username { get; init; }
    
    [Required, StringLength(32 , MinimumLength = 8)]
    public required string Password { get; init; }
    
    [Required, EmailAddress, StringLength(255)]
    public required string Email { get; init; }
    
    [StringLength(100, MinimumLength = 1)]
    public string? DisplayName { get; init; }
}
