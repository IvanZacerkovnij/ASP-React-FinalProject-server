using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Comments;

public class UpdateCommentRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public required string Content { get; init; }
}
