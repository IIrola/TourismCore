using Microsoft.EntityFrameworkCore;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Organizations;

namespace Tourism.Infrastructure.Persistence.Repositories;

/// <summary>EF Core-backed <see cref="ITourismOrganizationProfileRepository"/>.</summary>
public sealed class TourismOrganizationProfileRepository(TourismDbContext dbContext)
    : ITourismOrganizationProfileRepository
{
    public Task<TourismOrganizationProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.TourismOrganizationProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<TourismOrganizationProfile?> GetByOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
        => dbContext.TourismOrganizationProfiles
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId, cancellationToken);

    public Task AddAsync(TourismOrganizationProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.TourismOrganizationProfiles.Add(profile);
        return Task.CompletedTask;
    }
}
