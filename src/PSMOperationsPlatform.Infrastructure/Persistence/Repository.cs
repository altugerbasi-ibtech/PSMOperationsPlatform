using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class Repository<TEntity>(OperationsDbContext dbContext) : IRepository<TEntity>
    where TEntity : Entity
{
    public async Task<TEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<TEntity>()
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
