using FluentAssertions;
using NSubstitute;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Commands;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Organizations;

public class RecordProofOfLifeCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public RecordProofOfLifeCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(true);
    }

    private RecordProofOfLifeCommandHandler Handler() => new(_profiles, _unitOfWork, _clock, _currentUser);

    private TourismOrganizationProfile GivenRegisteredOperator()
    {
        var profile = TourismOrganizationProfile.Create(_organizationId, _tenantId, TourismProfileType.Operator, "tour-guide", Now.AddYears(-1));
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    [Fact]
    public async Task Records_proof_of_life_at_the_current_time()
    {
        var profile = GivenRegisteredOperator();

        var result = await Handler().Handle(new RecordProofOfLifeCommand(_organizationId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        profile.LastProofOfLifeAtUtc.Should().Be(Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unregistered_organization_is_reported_as_not_found()
    {
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns((TourismOrganizationProfile?)null);

        var result = await Handler().Handle(new RecordProofOfLifeCommand(_organizationId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileNotFound);
    }

    [Fact]
    public async Task A_caller_outside_the_organizations_scope_is_refused()
    {
        var profile = GivenRegisteredOperator();
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(false);

        var result = await Handler().Handle(new RecordProofOfLifeCommand(_organizationId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
        profile.LastProofOfLifeAtUtc.Should().BeNull();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
