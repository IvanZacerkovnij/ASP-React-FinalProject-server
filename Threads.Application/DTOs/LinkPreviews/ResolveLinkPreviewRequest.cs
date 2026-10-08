using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.LinkPreviews;

public sealed class ResolveLinkPreviewRequest
{
    [Required, Url, StringLength(2048, MinimumLength = 1)]
    public required string Url { get; init; }
}
