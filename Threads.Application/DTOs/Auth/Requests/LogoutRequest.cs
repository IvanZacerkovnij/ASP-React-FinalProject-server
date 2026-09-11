using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public class LogoutRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}
