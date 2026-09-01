using Tourism.Domain.Organizations;

namespace Tourism.Application.Organizations.Ports;

public interface ITourismOrganizationProfileRepository
{
    Task<TourismOrganizationProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tourism profile for a Platform organization. One organization has at most one
    /// tourism profile — it either takes part in this vertical or it does not.
    /// </summary>
    Task<TourismOrganizationProfile?> GetByOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task AddAsync(TourismOrganizationProfile profile, CancellationToken cancellationToken = default);
}
