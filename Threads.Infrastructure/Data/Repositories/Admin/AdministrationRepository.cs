using Threads.Application.Interfaces.Admin;

namespace Threads.Infrastructure.Data.Repositories.Admin;

public sealed partial class AdministrationRepository : IAdministrationRepository
{
    private readonly ThreadsDbContext _dbContext;

    public AdministrationRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}

