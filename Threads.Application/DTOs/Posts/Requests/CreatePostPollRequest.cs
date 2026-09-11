using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Posts.Requests;

public class CreatePostPollRequest
{
    [Required, MinLength(2), ValidPollOptions]
    public IReadOnlyCollection<string> Options { get; init; } = [];

    [FutureDateTime]
    public DateTime? EndsAt { get; init; }
}
