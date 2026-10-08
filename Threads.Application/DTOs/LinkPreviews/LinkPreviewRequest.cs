using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.LinkPreviews;

public sealed class LinkPreviewRequest
{
    [Required, Url, StringLength(2048, MinimumLength = 1)]
    public required string Url { get; init; }

    [StringLength(255, MinimumLength = 1)]
    public string? Title { get; init; }

    [Url, StringLength(2048, MinimumLength = 1)]
    public string? ImageUrl { get; init; }
}
