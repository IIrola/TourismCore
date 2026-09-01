using Tourism.Application.Common.Ports;

namespace Tourism.Infrastructure.Persistence;

/// <summary>
/// Commits everything the repositories added or mutated on <see cref="TourismDbContext"/> in
/// one transaction. Repositories never call SaveChanges themselves; only the handler,
/// through this, decides when a use case's work becomes durable.
/// </summary>
public sealed class UnitOfWork(TourismDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
