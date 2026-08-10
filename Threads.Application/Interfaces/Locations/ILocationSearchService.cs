using Threads.Application.DTOs.Locations;

namespace Threads.Application.Interfaces.Locations;

public interface ILocationSearchService
{
    Task<IReadOnlyCollection<LocationResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
