namespace Tourism.Application.Common.Ports;

/// <summary>
/// The scope a Platform-issued access token acts in — mirrors the "sco" claim's three
/// values. There is deliberately no BusinessLine case here: Tourism only ever needs to know
/// whether a caller acts platform-wide, for one tenant, or for one organization, and the
/// enum should not grow cases nothing here reads.
/// </summary>
public enum TourismScopeType
{
    Platform,
    Tenant,
    Organization
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
}
