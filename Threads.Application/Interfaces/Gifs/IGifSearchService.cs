using Threads.Application.DTOs.Gifs;

namespace Threads.Application.Interfaces.Gifs;

public interface IGifSearchService
{
    Task<IReadOnlyCollection<GifResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
