using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class StartPasswordChangeRequest
{
    [Required,StringLength(32 , MinimumLength = 8)]
    public required string CurrentPassword { get; init; }
    
    [Required,StringLength(32 , MinimumLength = 8)]
    public required string NewPassword { get; init; }
}