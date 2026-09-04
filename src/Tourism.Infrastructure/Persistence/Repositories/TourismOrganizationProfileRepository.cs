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

    /// <summary>
    /// The profile behind a public identifier.
    ///
    /// Untracked: it serves an anonymous read and nothing about it is going to be written
    /// back, so tracking it would only cost the change tracker work per public page view.
    /// </summary>
    public Task<TourismOrganizationProfile?> GetByPublicDirectoryIdAsync(
        string publicDirectoryId, CancellationToken cancellationToken = default)
        => dbContext.TourismOrganizationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicDirectoryId == publicDirectoryId, cancellationToken);

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
