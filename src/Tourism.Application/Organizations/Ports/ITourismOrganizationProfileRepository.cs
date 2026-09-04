using Tourism.Domain.Organizations;

namespace Tourism.Application.Organizations.Ports;

public interface ITourismOrganizationProfileRepository
{
    Task<TourismOrganizationProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tourism profile for a Platform organization. One organization has at most one
    /// tourism profile — it either takes part in this vertical or it does not.
    /// </summary>
    /// <summary>
    /// The profile behind a public directory identifier, for serving the public page.
    ///
    /// Only this direction is offered. "Give me the identifier for this profile" is a
    /// question the public page never asks and would let a caller holding an organization id
    /// mint public links.
    /// </summary>
    Task<TourismOrganizationProfile?> GetByPublicDirectoryIdAsync(
        string publicDirectoryId, CancellationToken cancellationToken = default);

    Task<TourismOrganizationProfile?> GetByOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task AddAsync(TourismOrganizationProfile profile, CancellationToken cancellationToken = default);
}
