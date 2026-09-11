using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Users;

public sealed class UserFileUploadRequest
{
    [Required]
    public required Stream Content { get; init; }

    [Required, StringLength(255, MinimumLength = 1)]
    public required string FileName { get; init; }

    [Required, RegularExpression(@"^image/(jpeg|png|webp)$"), StringLength(255)]
    public required string ContentType { get; init; }

    [Range(typeof(long), "1", "10485760")]
    public long SizeInBytes { get; init; }
}
