using Threads.Application.DTOs.LinkPreviews;

namespace Threads.Application.Interfaces.LinkPreviews;

public interface ILinkPreviewService
{
    Task<LinkPreviewResponse> ResolveAsync(
        string url,
        CancellationToken cancellationToken = default);
}
