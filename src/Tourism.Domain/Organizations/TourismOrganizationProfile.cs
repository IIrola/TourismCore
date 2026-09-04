using Tourism.Domain.Badges;
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

    /// <summary>
    /// Platform tenant that owns <see cref="OrganizationId"/>, captured when the organization
    /// joined this business line.
    ///
    /// This is a copy of a Platform fact, which normally invites drift — but not here: an
    /// organization's tenant is assigned when it is created and Platform exposes no way to
    /// change it, so the value cannot go stale. It is stored because isolation checks need it
    /// on every request, and asking Platform each time would put it on the critical path of
    /// every read.
    ///
    /// If Platform ever gains the ability to move an organization between tenants, this
    /// snapshot silently becomes wrong and the isolation check with it. Whoever adds that
    /// capability has to revisit this field.
    /// </summary>
    public Guid TenantId { get; private set; }

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

    /// <summary>Badge last decided for this operator, or null before the first assessment.</summary>
    public TourismBadge? CurrentBadge { get; private set; }

    public DateTime? BadgeAssessedAtUtc { get; private set; }

    /// <summary>The identity evaluation the current badge was decided from. Opaque: PIMA's id.</summary>
    public Guid? LastEvaluationId { get; private set; }

    /// <summary>
    /// The identifier PIMA publishes this operator's identity facts under, once it has one.
    ///
    /// Learned from an assessment rather than minted here, and that direction matters: one
    /// person appearing in two verticals has one public identity, not one per vertical. What
    /// BIT owns is the tourism page; who the page is about is the engine's identifier.
    ///
    /// Null until the operator has consented to publishing anything, which is also what makes
    /// the public page unavailable — the operator never asked for one.
    /// </summary>
    public string? PublicDirectoryId { get; private set; }

    /// <summary>
    /// Why the current badge is what it is, stored alongside it.
    ///
    /// A badge kept without its reasons cannot answer the one question an operator will
    /// actually ask. The legacy's shield was a number on a row with no record of how it got
    /// there, which left support with nothing to say.
    /// </summary>
    public string? BadgeReasons { get; private set; }

    private TourismOrganizationProfile() { }

    public static TourismOrganizationProfile Create(
        Guid organizationId,
        Guid tenantId,
        TourismProfileType profileType,
        string categoryCode,
        DateTime nowUtc)
    {
        DomainException.ThrowIfEmpty(organizationId, nameof(organizationId));
        DomainException.ThrowIfEmpty(tenantId, nameof(tenantId));
        DomainException.ThrowIfNullOrWhiteSpace(categoryCode, nameof(categoryCode));

        return new TourismOrganizationProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TenantId = tenantId,
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

    /// <summary>
    /// Stores the outcome of a badge assessment, so the directory can render a listing
    /// without asking the identity engine on every page view.
    /// </summary>
    /// <summary>
    /// Records the identifier PIMA publishes this operator under.
    ///
    /// Overwritten rather than fixed, because PIMA is the authority on it and an operator who
    /// withdraws and later publishes again keeps the same one — so a change here means the
    /// engine changed its answer, not that this profile drifted.
    /// </summary>
    public void RecordPublicDirectoryId(string? publicDirectoryId)
        => PublicDirectoryId = string.IsNullOrWhiteSpace(publicDirectoryId) ? null : publicDirectoryId.Trim();

    public void RecordBadge(BadgeDecision decision, Guid evaluationId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(decision);

        CurrentBadge = decision.Badge;
        BadgeAssessedAtUtc = nowUtc;
        LastEvaluationId = evaluationId;
        BadgeReasons = string.Join(" ", decision.Reasons);
    }

    /// <summary>How long since the operator last showed signs of trading, if ever.</summary>
    public TimeSpan? TimeSinceProofOfLife(DateTime nowUtc)
        => LastProofOfLifeAtUtc is { } last ? nowUtc - last : null;
}
