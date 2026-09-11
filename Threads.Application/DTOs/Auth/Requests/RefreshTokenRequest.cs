using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public class RefreshTokenRequest
{
    [Required, StringLength(44, MinimumLength = 44)]
    public required string RefreshToken { get; init; }
}
