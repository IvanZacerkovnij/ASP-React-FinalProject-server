using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Comments;

public class CreateCommentRequest
{
    [NotEmptyGuid]
    public Guid PostId { get; init; }

    [NotEmptyGuid]
    public Guid? ParentCommentId { get; init; }

    [Required, StringLength(1000, MinimumLength = 1)]
    public required string Content { get; init; }
}
