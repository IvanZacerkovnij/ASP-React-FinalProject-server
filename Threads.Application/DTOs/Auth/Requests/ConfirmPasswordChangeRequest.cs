using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Auth.Requests;

public sealed class ConfirmPasswordChangeRequest
{
    [Required, RegularExpression(@"^\d{6}$")]
    public required string Code { get; init; }
}
