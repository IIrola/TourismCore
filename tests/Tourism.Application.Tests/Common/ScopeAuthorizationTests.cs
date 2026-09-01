using FluentAssertions;
using NSubstitute;
using Tourism.Application.Common;
using Tourism.Application.Common.Ports;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Common;

/// <summary>
/// Who may act on a tourism profile.
///
/// The owning tenant always comes from the stored profile, never from the request — a
/// caller-supplied tenant id would let anyone pass their own and clear the check for someone
/// else's organization.
/// </summary>
public class ScopeAuthorizationTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private TourismOrganizationProfile Profile() => TourismOrganizationProfile.Create(
        _organizationId, _tenantId, TourismProfileType.Operator, "tour-guide", Now);

    [Fact]
    public void The_check_reads_the_tenant_from_the_profile_not_from_anywhere_else()
    {
        var profile = Profile();
        _currentUser.CanActOnOrganization(_organizationId, _tenantId).Returns(true);

        ScopeAuthorization.CanActOn(_currentUser, profile).Should().BeTrue();

        // The exact pair from the stored row, so a request cannot substitute its own tenant.
        _currentUser.Received(1).CanActOnOrganization(profile.OrganizationId, profile.TenantId);
    }

    [Fact]
    public void A_caller_who_cannot_reach_the_organization_is_refused()
    {
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(false);

        ScopeAuthorization.CanActOn(_currentUser, Profile()).Should().BeFalse();
    }

    [Fact]
    public void A_tenant_scoped_caller_of_the_owning_tenant_is_now_allowed()
    {
        // The interim check could not admit this case: without the profile's tenant there was
        // nothing trustworthy to compare a tenant-scoped token against, so it failed closed.
        var profile = Profile();
        _currentUser.CanActOnOrganization(profile.OrganizationId, profile.TenantId).Returns(true);

        ScopeAuthorization.CanActOn(_currentUser, profile).Should().BeTrue();
    }
}
