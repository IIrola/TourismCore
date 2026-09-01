using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tourism.Application.Common.Ports;

namespace Tourism.Infrastructure.Security;

/// <summary>
/// Reads the caller's identity back from the claims Platform put on the access token —
/// mirrors <c>Platform.Infrastructure.Authorization.HttpContextCurrentUser</c>, which already
/// went through fixing a fail-open here: an earlier version of that class returned the
/// platform scope for an anonymous request, which made every authorization check answer true
/// for everyone. BIT copies its corrected design rather than the original.
///
/// Every member fails closed. An anonymous caller must never reach an affirmative answer
/// here, because these checks are the last line between a route that forgot its
/// authorization and the data behind it. Unlike Platform's own version, an unrecognized "sco"
/// value is treated as "no scope" rather than thrown: BIT trusts tokens it did not issue, and
/// a token this API cannot make sense of must fail closed, not crash the request.
///
/// Requires the JWT bearer handler to be configured with <c>MapInboundClaims = false</c>
/// (see Program.cs); otherwise ASP.NET Core rewrites "sub" into a long legacy claim-type URI
/// before this class ever sees it.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
        => IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : Guid.Empty;

    public Guid? TenantId
        => IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue("tnt"), out var id) ? id : null;

    public TourismScopeType? ScopeType
    {
        get
        {
            // Captured once: Principal re-reads HttpContext on every access.
            var principal = Principal;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            return principal.FindFirstValue("sco") switch
            {
                "platform" => TourismScopeType.Platform,
                "tenant" => TourismScopeType.Tenant,
                "organization" => TourismScopeType.Organization,
                // Includes "businessline" (Platform issues it for other verticals) and any
                // value this API does not recognize. Failing closed here, not throwing, keeps
                // a token BIT cannot fully interpret from crashing an otherwise-authenticated
                // request; it simply carries no scope BIT can act on.
                _ => null
            };
        }
    }

    public Guid? ScopeId
        => IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue("sid"), out var id) ? id : null;

    public bool CanActOnOrganization(Guid organizationId, Guid owningTenantId)
        => ScopeType switch
        {
            null => false,
            TourismScopeType.Platform => true,
            TourismScopeType.Tenant => ScopeId == owningTenantId,
            TourismScopeType.Organization => ScopeId == organizationId,
            _ => false
        };
}
