using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Posts.Requests;

public class PostEmbedRequest
{
    [Required, Url, StringLength(2048, MinimumLength = 1)]
    public required string Url { get; init; }

    [StringLength(255, MinimumLength = 1)]
    public string? Title { get; init; }

    [StringLength(1000, MinimumLength = 1)]
    public string? Description { get; init; }

    [Url, StringLength(2048, MinimumLength = 1)]
    public string? ThumbnailUrl { get; init; }
}
