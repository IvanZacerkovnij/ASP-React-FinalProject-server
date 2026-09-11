using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}