using Tourism.Application.Common.Ports;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Common;

/// <summary>
/// Authorizes a caller against a tourism profile that already exists.
///
/// The owning tenant always comes from the stored profile, never from the request. A
/// caller-supplied tenant id would let anyone pass their own and clear the check for someone
/// else's organization, which is the exact hole this check exists to close.
/// </summary>
public static class ScopeAuthorization
{
    public static bool CanActOn(ICurrentUser currentUser, TourismOrganizationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(profile);

        return currentUser.CanActOnOrganization(profile.OrganizationId, profile.TenantId);
    }
}
