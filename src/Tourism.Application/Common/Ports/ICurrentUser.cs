namespace Tourism.Application.Common.Ports;

/// <summary>
/// The scope a Platform-issued access token acts in — mirrors the "sco" claim.
///
/// <see cref="BusinessLine"/> was deliberately absent while Platform could not issue such a
/// token; now that it can, it is the scope that matters most here. It says the caller is
/// acting inside one organization's participation in one vertical, and the accompanying "bl"
/// claim says which vertical — so a caller acting in some other business line is recognized
/// and refused rather than silently treated as having no scope at all.
/// </summary>
public enum TourismScopeType
{
    Platform,
    Tenant,
    Organization,
    BusinessLine
}

/// <summary>
/// The caller of the current request, as established by the access token Platform issued.
///
/// BIT does not authenticate or authorize on its own terms: Platform owns that mechanism.
/// This port only reads back what a token already asserts, the same way
/// Platform.Infrastructure.Authorization.HttpContextCurrentUser reads its own tokens — BIT
/// has no project reference to Platform, so the claim names ("sub", "sco", "sid", "tnt") are
/// the entire contract between the two.
///
/// Every member fails closed. An unauthenticated caller must never reach an affirmative
/// answer here: a route that forgot its authorization is the last line these checks defend.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The "sub" claim, or <see cref="Guid.Empty"/> when there is no authenticated caller.</summary>
    Guid UserId { get; }

    /// <summary>The "tnt" claim — the tenant owning the caller's scope, when there is one.</summary>
    Guid? TenantId { get; }

    /// <summary>The "sco" claim, or null when there is no authenticated caller.</summary>
    TourismScopeType? ScopeType { get; }

    /// <summary>The "sid" claim — the id of the scope instance, absent only for the platform scope.</summary>
    Guid? ScopeId { get; }

    /// <summary>
    /// True when the caller's scope authorizes acting on the given organization: the token is
    /// scoped to it directly, to the tenant that owns it, or platform-wide. Always false when
    /// there is no authenticated caller.
    ///
    /// <paramref name="owningTenantId"/> must come from a record BIT already trusts (never
    /// from the same request the caller is trying to get past) — otherwise a caller could pass
    /// their own tenant id and let themselves through regardless of who actually owns the
    /// organization.
    /// </summary>
    bool CanActOnOrganization(Guid organizationId, Guid owningTenantId);

    /// <summary>
    /// The "bl" claim — which business line the caller is acting in, when the scope is a
    /// participation. Null otherwise.
    /// </summary>
    string? BusinessLineCode { get; }

    /// <summary>
    /// The "org" claim — the organization holding the participation the caller is scoped to.
    /// Null for every other scope, where the organization is named by <see cref="ScopeId"/>
    /// or not named at all.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// True when the caller is acting specifically in this organization's tourism
    /// participation.
    ///
    /// Distinct from <see cref="CanActOnOrganization"/>, which asks whether the caller reaches
    /// the company at all. This asks a narrower question: is the caller here <i>as tourism</i>?
    /// That is what proves the organization actually took part in this business line, without
    /// BIT having to ask Platform — Platform refuses to issue such a token for a withdrawn
    /// participation, an archived organization or a suspended tenant, so the claim is a
    /// stronger guarantee than a lookup BIT could make on its own, and it is re-checked every
    /// time a token is issued or refreshed.
    /// </summary>
    bool ActsInTourismFor(Guid organizationId);
}
