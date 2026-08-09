using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Media;

namespace Threads.Infrastracture.Data.Repositories.Media;

public class MediaRepository : IMediaRepository
{
    private readonly ThreadsDbContext _dbContext;

    public MediaRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domain.Entities.Media?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Medias
            .FirstOrDefaultAsync(media => media.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Domain.Entities.Media>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Medias
            .Where(media => ids.Contains(media.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Media media, CancellationToken cancellationToken = default)
    {
        await _dbContext.Medias.AddAsync(media, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
