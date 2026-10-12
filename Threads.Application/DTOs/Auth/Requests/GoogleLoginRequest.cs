using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class GoogleLoginRequest
{
    [Required, StringLength(8192, MinimumLength = 1)]
    public required string IdToken { get; init; }
}
