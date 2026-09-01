using FluentAssertions;
using NSubstitute;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Commands;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Organizations;

/// <summary>
/// Registration is the one place a caller-asserted tenant id is trusted at all (see the
/// XML doc on <see cref="RegisterTourismProfileCommand"/>): nothing yet claims the
/// organization for tourism, so there is no stored owner a forged id could impersonate.
/// </summary>
public class RegisterTourismProfileCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public RegisterTourismProfileCommandHandlerTests() => _clock.UtcNow.Returns(Now);

    private RegisterTourismProfileCommandHandler Handler() => new(_profiles, _unitOfWork, _clock, _currentUser);

    private RegisterTourismProfileCommand Command()
        => new(_tenantId, _organizationId, TourismProfileType.Operator, "tour-guide");

    private void GivenCallerIsAuthorized()
        => _currentUser.CanActOnOrganization(_organizationId, _tenantId).Returns(true);

    [Fact]
    public async Task A_new_organization_is_registered_as_Undetermined()
    {
        GivenCallerIsAuthorized();

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().Be(TourismBadge.Undetermined);
        result.Value.ProfileType.Should().Be(TourismProfileType.Operator);
        result.Value.CategoryCode.Should().Be("tour-guide");
        await _profiles.Received(1).AddAsync(Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_organization_already_registered_is_refused()
    {
        GivenCallerIsAuthorized();
        var existing = TourismOrganizationProfile.Create(_organizationId, _tenantId, TourismProfileType.Operator, "tour-guide", Now);
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileAlreadyExists);
        await _profiles.DidNotReceive().AddAsync(Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_caller_not_authorized_for_the_asserted_tenant_is_refused()
    {
        // CanActOnOrganization returns false by default on the substitute.
        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
        await _profiles.DidNotReceive().AddAsync(Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_tenant_id_is_rejected_before_any_authorization_check()
    {
        var command = new RegisterTourismProfileCommand(Guid.Empty, _organizationId, TourismProfileType.Operator, "tour-guide");

        var result = await Handler().Handle(command, CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.InvalidInput);
    }

    [Fact]
    public async Task An_invalid_category_code_surfaces_as_invalid_input()
    {
        GivenCallerIsAuthorized();
        var command = new RegisterTourismProfileCommand(_tenantId, _organizationId, TourismProfileType.Operator, "   ");

        var result = await Handler().Handle(command, CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.InvalidInput);
    }
}
