using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Interfaces.Media;

public interface IMediaRepository
{
    Task<IReadOnlyCollection<MediaEntity>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(MediaEntity media, CancellationToken cancellationToken = default);
}
