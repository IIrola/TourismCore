using Tourism.Domain.Common;

namespace Tourism.Domain.Organizations;

/// <summary>
/// What BIT knows about an organization that Platform does not.
///
/// Keyed by <see cref="OrganizationId"/>, an opaque reference to Platform's organization. BIT
/// never copies the organization itself — not its legal name, tax id or country, all of which
/// Platform owns. Duplicating them would create a second source of truth that drifts, and the
/// legacy shows exactly how that ends: its tourism classification lived on the identity user
/// record, so the identity aggregate could not be touched without touching tourism.
///
/// Only genuinely touristic attributes belong here.
/// </summary>
public sealed class TourismOrganizationProfile
{
    public Guid Id { get; private set; }

    /// <summary>Platform's organization. Opaque: never resolved from here.</summary>
    public Guid OrganizationId { get; private set; }

    public TourismProfileType ProfileType { get; private set; }

    /// <summary>Category from the tourism catalogue, e.g. "tour guide", "travel insurer".</summary>
    public string CategoryCode { get; private set; } = string.Empty;

    public DateTime RegisteredAtUtc { get; private set; }

    /// <summary>
    /// Last time the operator did something that proves they are still trading.
    ///
    /// Recovered from the legacy's "proof of life": in tourism a listing that has gone quiet
    /// is a risk of its own, independent of how well the identity behind it verified. A
    /// travel agency that checked out perfectly two years ago and has not been seen since is
    /// not the same proposition as one that was seen last week.
    /// </summary>
    public DateTime? LastProofOfLifeAtUtc { get; private set; }

    private TourismOrganizationProfile() { }

    public static TourismOrganizationProfile Create(
        Guid organizationId,
        TourismProfileType profileType,
        string categoryCode,
        DateTime nowUtc)
    {
        DomainException.ThrowIfEmpty(organizationId, nameof(organizationId));
        DomainException.ThrowIfNullOrWhiteSpace(categoryCode, nameof(categoryCode));

        return new TourismOrganizationProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProfileType = profileType,
            CategoryCode = categoryCode.Trim().ToLowerInvariant(),
            RegisteredAtUtc = nowUtc
        };
    }

    public void Reclassify(TourismProfileType profileType, string categoryCode)
    {
        DomainException.ThrowIfNullOrWhiteSpace(categoryCode, nameof(categoryCode));
        ProfileType = profileType;
        CategoryCode = categoryCode.Trim().ToLowerInvariant();
    }

    public void RecordProofOfLife(DateTime nowUtc) => LastProofOfLifeAtUtc = nowUtc;

    /// <summary>How long since the operator last showed signs of trading, if ever.</summary>
    public TimeSpan? TimeSinceProofOfLife(DateTime nowUtc)
        => LastProofOfLifeAtUtc is { } last ? nowUtc - last : null;
}
