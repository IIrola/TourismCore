using FluentAssertions;
using NSubstitute;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Application.Organizations.Queries;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Organizations;

public class GetCurrentBadgeQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetCurrentBadgeQueryHandlerTests() => _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(true);

    private GetCurrentBadgeQueryHandler Handler() => new(_profiles, _currentUser);

    private TourismOrganizationProfile GivenRegisteredOperator()
    {
        var profile = TourismOrganizationProfile.Create(_organizationId, _tenantId, TourismProfileType.Operator, "tour-guide", Now);
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    [Fact]
    public async Task A_never_assessed_profile_reads_as_Undetermined_not_as_a_failure()
    {
        GivenRegisteredOperator();

        var result = await Handler().Handle(new GetCurrentBadgeQuery(_organizationId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().Be(TourismBadge.Undetermined);
        result.Value.AssessedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task An_assessed_profile_reports_its_recorded_badge()
    {
        var profile = GivenRegisteredOperator();
        var evaluationId = Guid.NewGuid();
        profile.RecordBadge(new BadgeDecision(TourismBadge.Gold, ["Earned it."]), evaluationId, Now);

        var result = await Handler().Handle(new GetCurrentBadgeQuery(_organizationId), CancellationToken.None);

        result.Value!.Badge.Should().Be(TourismBadge.Gold);
        result.Value.EvaluationId.Should().Be(evaluationId);
    }

    [Fact]
    public async Task An_unregistered_organization_is_reported_as_not_found()
    {
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns((TourismOrganizationProfile?)null);

        var result = await Handler().Handle(new GetCurrentBadgeQuery(_organizationId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileNotFound);
    }

    [Fact]
    public async Task A_caller_outside_the_organizations_scope_is_refused()
    {
        GivenRegisteredOperator();
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(false);

        var result = await Handler().Handle(new GetCurrentBadgeQuery(_organizationId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
    }
}
