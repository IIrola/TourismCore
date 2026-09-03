using FluentAssertions;
using MediatR;
using NSubstitute;
using Tourism.Application.Badges.Commands;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Commands;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Organizations;

/// <summary>
/// BIT's half of onboarding: recognize the classification, record the listing, decide the
/// first badge.
///
/// The tenant and the organization come from the token rather than the request. That is what
/// closes the gap the previous version had to live with — it accepted a caller-asserted tenant
/// because no stored owner existed yet to check against.
/// </summary>
public class OnboardOrganizationCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public OnboardOrganizationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.ActsInTourismFor(_organizationId).Returns(true);
        _currentUser.TenantId.Returns(_tenantId);
        _currentUser.UserId.Returns(Guid.NewGuid());
        GivenAssessmentSucceeds(TourismBadge.Bronze);
    }

    private OnboardOrganizationCommandHandler Handler()
        => new(_profiles, _mediator, _unitOfWork, _clock, _currentUser);

    private OnboardOrganizationCommand Command(
        string category = "guides-and-activities",
        TourismProfileType type = TourismProfileType.Operator,
        bool withContacts = true)
        => new(
            _organizationId, type, category, "trace-onboarding-1",
            withContacts ? [new EvaluationContact(EvaluationChannel.Email, "operator@example.com")] : []);

    private void GivenAssessmentSucceeds(TourismBadge badge)
        => _mediator.Send(Arg.Any<AssessOperatorBadgeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<BadgeResponse>.Ok(new BadgeResponse(
                _organizationId, badge, ["Earned."], Now, Guid.NewGuid(), 700, 0.55m)));

    // ---------- the happy path ----------

    [Fact]
    public async Task An_operator_joins_tourism_and_is_assessed_in_the_same_step()
    {
        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Profile.CategoryCode.Should().Be("guides-and-activities");
        result.Value.Badge!.Badge.Should().Be(TourismBadge.Bronze);
        result.Value.Note.Should().BeNull();
        await _profiles.Received(1).AddAsync(
            Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_tenant_is_taken_from_the_token_not_from_the_request()
    {
        TourismOrganizationProfile? stored = null;
        await _profiles.AddAsync(
            Arg.Do<TourismOrganizationProfile>(p => stored = p), Arg.Any<CancellationToken>());

        await Handler().Handle(Command(), CancellationToken.None);

        stored!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task The_assessment_is_delegated_rather_than_reimplemented()
    {
        // One implementation owns the PIMA orchestration. Repeating it here is how two
        // versions of the same decision start to drift.
        AssessOperatorBadgeCommand? sent = null;
        await _mediator.Send(
            Arg.Do<AssessOperatorBadgeCommand>(c => sent = c), Arg.Any<CancellationToken>());

        await Handler().Handle(Command(), CancellationToken.None);

        sent!.OrganizationId.Should().Be(_organizationId);
        sent.TenantId.Should().Be(_tenantId);
        sent.CorrelationId.Should().Be("trace-onboarding-1");
    }

    // ---------- proof of participation ----------

    [Fact]
    public async Task A_caller_not_acting_in_tourism_is_refused()
    {
        // Someone who merely administers the company, without entering it into the tourism
        // business line, has no token that proves the participation exists.
        _currentUser.ActsInTourismFor(Arg.Any<Guid>()).Returns(false);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
        await _profiles.DidNotReceive().AddAsync(
            Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_token_naming_a_different_organization_is_refused()
    {
        var someoneElse = Guid.NewGuid();
        _currentUser.ActsInTourismFor(someoneElse).Returns(false);

        var result = await Handler().Handle(
            Command() with { OrganizationId = someoneElse }, CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
    }

    [Fact]
    public async Task A_token_without_an_owning_tenant_is_refused()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
    }

    // ---------- classification is a tourism rule ----------

    [Fact]
    public async Task A_category_outside_the_catalogue_is_refused()
    {
        // The legacy's anonymous sign-up stored whatever integer arrived, including zero,
        // which exists in no catalogue.
        var result = await Handler().Handle(Command(category: "space-tourism"), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.InvalidInput);
        await _profiles.DidNotReceive().AddAsync(
            Arg.Any<TourismOrganizationProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_category_belonging_to_the_other_kind_of_participant_is_refused()
    {
        var result = await Handler().Handle(
            Command(category: "lodging", type: TourismProfileType.Traveller), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.InvalidInput);
    }

    [Fact]
    public async Task A_traveller_can_be_classified_under_a_traveller_category()
    {
        var result = await Handler().Handle(
            Command(category: "national-traveller", type: TourismProfileType.Traveller), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ---------- joining twice ----------

    [Fact]
    public async Task An_organization_already_in_tourism_is_not_onboarded_again()
    {
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns(TourismOrganizationProfile.Create(
                _organizationId, _tenantId, TourismProfileType.Operator, "lodging", Now));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileAlreadyExists);
    }

    // ---------- the assessment is not allowed to undo the onboarding ----------

    [Fact]
    public async Task An_unreachable_identity_engine_still_leaves_the_organization_onboarded()
    {
        // The organization has joined tourism either way; refusing the whole onboarding would
        // undo work that already succeeded because a dependency blinked.
        _mediator.Send(Arg.Any<AssessOperatorBadgeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<BadgeResponse>.Fail(
                TourismErrorCodes.IdentityServiceUnavailable, "Connection refused."));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().BeNull();
        result.Value.Note.Should().Contain(TourismErrorCodes.IdentityServiceUnavailable);
        result.Value.Profile.Badge.Should().Be(TourismBadge.Undetermined);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Onboarding_without_contacts_records_the_listing_and_says_why_there_is_no_badge()
    {
        var result = await Handler().Handle(Command(withContacts: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().BeNull();
        result.Value.Note.Should().Contain("No contacts");
        await _mediator.DidNotReceive().Send(
            Arg.Any<AssessOperatorBadgeCommand>(), Arg.Any<CancellationToken>());
    }
}

/// <summary>The tourism catalogue itself — a business rule, so it is tested as one.</summary>
public class TourismCategoriesTests
{
    [Fact]
    public void Every_category_is_addressed_by_a_stable_lowercase_code()
    {
        TourismCategories.All.Should().OnlyContain(c => c.Code == c.Code.ToLowerInvariant());
        TourismCategories.All.Select(c => c.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Lookup_is_case_and_whitespace_insensitive()
    {
        TourismCategories.Find("  LODGING  ")!.Code.Should().Be("lodging");
    }

    [Fact]
    public void Both_kinds_of_participant_have_somewhere_to_be_classified()
    {
        TourismCategories.For(TourismProfileType.Traveller).Should().NotBeEmpty();
        TourismCategories.For(TourismProfileType.Operator).Should().NotBeEmpty();
    }

    [Fact]
    public void A_category_never_permits_the_wrong_kind_of_participant()
    {
        foreach (var category in TourismCategories.All)
        {
            var other = category.AppliesTo == TourismProfileType.Operator
                ? TourismProfileType.Traveller
                : TourismProfileType.Operator;

            TourismCategories.Permits(category.AppliesTo, category.Code).Should().BeTrue();
            TourismCategories.Permits(other, category.Code).Should().BeFalse();
        }
    }

    [Fact]
    public void An_unknown_or_missing_code_permits_nothing()
    {
        TourismCategories.Permits(TourismProfileType.Operator, null).Should().BeFalse();
        TourismCategories.Permits(TourismProfileType.Operator, "  ").Should().BeFalse();
        TourismCategories.Permits(TourismProfileType.Operator, "nope").Should().BeFalse();
    }
}
