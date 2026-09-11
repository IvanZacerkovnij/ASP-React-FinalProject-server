using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Posts.Requests;

public class CreatePostRequest
{
    [StringLength(2000, MinimumLength = 1)]
    public string? Content { get; init; }

    [Required, UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public CreatePostPollRequest? Poll { get; init; }

    public PostLocationRequest? Location { get; init; }

    public PostEmbedRequest? Embed { get; init; }
}
