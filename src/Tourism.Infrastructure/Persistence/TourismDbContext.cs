using Microsoft.EntityFrameworkCore;
using Tourism.Domain.Organizations;

namespace Tourism.Infrastructure.Persistence;

/// <summary>
/// The BIT bounded context's unit of work.
///
/// Deliberately derives from plain <see cref="DbContext"/>, the same choice Platform and
/// PIMA make. BIT owns tourism tables only — everything it knows about a Platform
/// organization or a PIMA evaluation arrives as an opaque id, never as a foreign key or a
/// join reaching into another context's schema.
/// </summary>
public sealed class TourismDbContext(DbContextOptions<TourismDbContext> options) : DbContext(options)
{
    public DbSet<TourismOrganizationProfile> TourismOrganizationProfiles => Set<TourismOrganizationProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TourismDbContext).Assembly);
    }
}
