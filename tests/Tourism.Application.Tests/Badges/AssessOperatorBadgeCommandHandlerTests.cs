using FluentAssertions;
using NSubstitute;
using Tourism.Application.Badges.Commands;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.Badges;

/// <summary>
/// The flow that crosses all three services: Platform authorized the caller and issued the
/// service token, PIMA states what the evidence supports, BIT decides what that means for a
/// listing. These tests cover BIT's half — including what it does when the engine is down,
/// which is the case a distributed system will actually hit.
/// </summary>
public class AssessOperatorBadgeCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();
    private readonly IIdentityEvaluationClient _identity = Substitute.For<IIdentityEvaluationClient>();
    private readonly IPossessionClient _possession = Substitute.For<IPossessionClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Guid _organizationId = Guid.NewGuid();

    /// <summary>Tenant the caller asserts in the request — forwarded to PIMA as context.</summary>
    private readonly Guid _tenantId = Guid.NewGuid();

    /// <summary>Tenant actually recorded on the profile. Deliberately different from the one
    /// the request carries, so a check against the wrong one would fail visibly.</summary>
    private readonly Guid _profileTenantId = Guid.NewGuid();

    public AssessOperatorBadgeCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        // Authorized by default: most of these tests are about the badge decision itself,
        // not about scope. The rejection cases below override this explicitly.
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(true);

        // Platform vouches for nothing by default, so each test states the possession it
        // actually exercises.
        _possession.GetConfirmedAsync(Arg.Any<IReadOnlyList<EvaluationContact>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ConfirmedContact>>.Ok([]));
    }

    private AssessOperatorBadgeCommandHandler Handler()
        => new(_profiles, _identity, _possession, _unitOfWork, _clock, _currentUser);

    private TourismOrganizationProfile GivenRegisteredOperator(DateTime? proofOfLife = null)
    {
        var profile = TourismOrganizationProfile.Create(
            _organizationId, _profileTenantId, TourismProfileType.Operator, "tour-guide", Now.AddYears(-2));
        profile.RecordProofOfLife(proofOfLife ?? Now.AddDays(-10));
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private void GivenIdentityAnswers(
        int? score, decimal coverage = 1m, ReportStanding standing = ReportStanding.None)
        => _identity.EvaluateAsync(Arg.Any<IdentityEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdentityEvaluationOutcome>.Ok(
                new IdentityEvaluationOutcome(
                    Guid.NewGuid(), new IdentityAssessment(score, coverage, Now), standing)));

    private AssessOperatorBadgeCommand Command()
        => new(_tenantId, _organizationId, "trace-badge-1",
            [new EvaluationContact(EvaluationChannel.Email, "operator@example.com")]);

    [Fact]
    public async Task A_strong_identity_with_full_coverage_earns_Gold()
    {
        GivenRegisteredOperator();
        GivenIdentityAnswers(900);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().Be(TourismBadge.Gold);
        result.Value.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task The_decision_is_recorded_on_the_profile()
    {
        // The directory renders listings without asking the identity engine on every view.
        var profile = GivenRegisteredOperator();
        GivenIdentityAnswers(900);

        await Handler().Handle(Command(), CancellationToken.None);

        profile.CurrentBadge.Should().Be(TourismBadge.Gold);
        profile.BadgeAssessedAtUtc.Should().Be(Now);
        profile.LastEvaluationId.Should().NotBeNull();
        profile.BadgeReasons.Should().NotBeNullOrWhiteSpace("a stored badge has to be able to explain itself");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_organization_outside_the_tourism_line_is_refused()
    {
        _profiles.GetByOrganizationAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns((TourismOrganizationProfile?)null);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileNotFound);
        await _identity.DidNotReceive().EvaluateAsync(
            Arg.Any<IdentityEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_request_with_no_contacts_never_reaches_the_engine()
    {
        var command = new AssessOperatorBadgeCommand(_tenantId, _organizationId, "trace", []);

        var result = await Handler().Handle(command, CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.InvalidInput);
        await _identity.DidNotReceive().EvaluateAsync(
            Arg.Any<IdentityEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    // ---------- the case a distributed system actually hits ----------

    [Fact]
    public async Task An_unreachable_engine_leaves_the_previous_badge_untouched()
    {
        // Silently downgrading an operator because a dependency was down would publish a claim
        // about them that nothing supports.
        var profile = GivenRegisteredOperator();
        profile.RecordBadge(new BadgeDecision(TourismBadge.Gold, ["Earned earlier."]), Guid.NewGuid(), Now.AddDays(-1));
        _identity.EvaluateAsync(Arg.Any<IdentityEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<IdentityEvaluationOutcome>.Fail(
                TourismErrorCodes.IdentityServiceUnavailable, "Connection refused."));

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(TourismErrorCodes.IdentityServiceUnavailable);
        profile.CurrentBadge.Should().Be(TourismBadge.Gold, "the outage is not a verdict about the operator");
        profile.BadgeAssessedAtUtc.Should().Be(Now.AddDays(-1));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- BIT preserves what PIMA distinguishes ----------

    [Fact]
    public async Task An_inconclusive_evaluation_is_not_turned_into_a_failed_one()
    {
        GivenRegisteredOperator();
        GivenIdentityAnswers(score: null, coverage: 0m);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value!.Badge.Should().Be(TourismBadge.Undetermined);
        result.Value.IdentityScore.Should().BeNull();
    }

    [Fact]
    public async Task Thin_coverage_holds_the_badge_back_even_on_a_strong_score()
    {
        GivenRegisteredOperator();
        GivenIdentityAnswers(950, coverage: 0.20m);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value!.Badge.Should().Be(TourismBadge.Bronze);
        result.Value.IdentityCoverage.Should().Be(0.20m);
    }

    [Fact]
    public async Task An_operator_gone_quiet_is_held_back_regardless_of_identity()
    {
        GivenRegisteredOperator(proofOfLife: Now.AddYears(-2));
        GivenIdentityAnswers(950);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value!.Badge.Should().Be(TourismBadge.Bronze);
        result.Value.Reasons.Should().Contain(r => r.Contains("trading"));
    }

    // ---------- the contract with PIMA ----------

    [Fact]
    public async Task The_caller_context_is_passed_through_to_the_engine()
    {
        // Correlation has to survive the hop, or a trace stops at the boundary.
        GivenRegisteredOperator();
        GivenIdentityAnswers(700);
        IdentityEvaluationRequest? sent = null;
        await _identity.EvaluateAsync(
            Arg.Do<IdentityEvaluationRequest>(r => sent = r), Arg.Any<CancellationToken>());

        await Handler().Handle(Command(), CancellationToken.None);

        sent!.TenantId.Should().Be(_tenantId);
        sent.OrganizationId.Should().Be(_organizationId);
        sent.CorrelationId.Should().Be("trace-badge-1");
    }

    [Fact]
    public async Task The_response_points_back_at_the_evidence()
    {
        // A disputed badge has to be traceable to what the providers actually said, not stop
        // at "the system decided".
        GivenRegisteredOperator();
        GivenIdentityAnswers(900);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value!.EvaluationId.Should().NotBeEmpty();
        result.Value.IdentityScore.Should().Be(900);
    }

    // ---------- possession is asked for, never taken from the caller ----------

    [Fact]
    public async Task What_Platform_vouches_for_is_forwarded_to_the_engine()
    {
        // It used to be a field on the request. That made the heaviest single input to an
        // identity score something the caller wrote about themselves.
        GivenRegisteredOperator();
        GivenIdentityAnswers(900);
        _possession.GetConfirmedAsync(Arg.Any<IReadOnlyList<EvaluationContact>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ConfirmedContact>>.Ok(
                [new ConfirmedContact(EvaluationChannel.Email, "operator@example.com", Now.AddDays(-3))]));

        IdentityEvaluationRequest? sent = null;
        await _identity.EvaluateAsync(
            Arg.Do<IdentityEvaluationRequest>(r => sent = r), Arg.Any<CancellationToken>());

        await Handler().Handle(Command(), CancellationToken.None);

        var asserted = sent!.AssertedPossession.Should().ContainSingle().Subject;
        asserted.Value.Should().Be("operator@example.com");
        asserted.LastConfirmedAtUtc.Should().Be(Now.AddDays(-3));
    }

    [Fact]
    public async Task Each_confirmed_contact_counts_once()
    {
        // Platform records that a contact was proven and when, not how many times somebody
        // re-proved it. Claiming more would inflate a dimension that saturates on repeats.
        GivenRegisteredOperator();
        GivenIdentityAnswers(900);
        _possession.GetConfirmedAsync(Arg.Any<IReadOnlyList<EvaluationContact>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ConfirmedContact>>.Ok(
                [new ConfirmedContact(EvaluationChannel.Email, "operator@example.com", Now)]));

        IdentityEvaluationRequest? sent = null;
        await _identity.EvaluateAsync(
            Arg.Do<IdentityEvaluationRequest>(r => sent = r), Arg.Any<CancellationToken>());

        await Handler().Handle(Command(), CancellationToken.None);

        sent!.AssertedPossession!.Single().ConfirmedCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unreachable_Platform_lowers_the_evidence_rather_than_stopping_the_assessment()
    {
        // The score then rests on less and says so through its coverage, which is truthful.
        // Refusing outright would let one dependency stop a listing being assessed at all, and
        // asserting possession nobody confirmed would be worse than either.
        GivenRegisteredOperator();
        GivenIdentityAnswers(900);
        _possession.GetConfirmedAsync(Arg.Any<IReadOnlyList<EvaluationContact>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ConfirmedContact>>.Fail(
                TourismErrorCodes.PlatformUnavailable, "Connection refused."));

        IdentityEvaluationRequest? sent = null;
        await _identity.EvaluateAsync(
            Arg.Do<IdentityEvaluationRequest>(r => sent = r), Arg.Any<CancellationToken>());

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sent!.AssertedPossession.Should().BeNull("nothing was confirmed, so nothing is claimed");
    }

    [Fact]
    public async Task The_command_has_no_possession_field_for_a_caller_to_fill_in()
    {
        typeof(AssessOperatorBadgeCommand).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Possession"));

        await Task.CompletedTask;
    }

    // ---------- scope: a caller may not act on an organization that is not theirs ----------

    [Fact]
    public async Task A_caller_who_cannot_reach_the_organization_is_refused()
    {
        GivenRegisteredOperator();
        _currentUser.CanActOnOrganization(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(false);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.Forbidden);
        await _identity.DidNotReceive().EvaluateAsync(
            Arg.Any<IdentityEvaluationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_scope_check_uses_the_profiles_own_tenant_not_the_requests()
    {
        // The command carries a TenantId, but it is only context forwarded to PIMA. Checking
        // against it would let a caller claim authority over someone else's organization just
        // by naming their own tenant.
        var profile = GivenRegisteredOperator();
        GivenIdentityAnswers(900);

        await Handler().Handle(Command(), CancellationToken.None);

        _currentUser.Received().CanActOnOrganization(profile.OrganizationId, profile.TenantId);
    }

    [Fact]
    public async Task An_upheld_report_reaching_BIT_from_PIMA_costs_the_operator_their_badge()
    {
        // The whole chain: PIMA publishes the standing beside the score, BIT reads it, and
        // BIT's own rule decides what it means. In the legacy the engine had already decided
        // for every vertical, by overwriting the score with zero.
        GivenRegisteredOperator();
        GivenIdentityAnswers(950, standing: ReportStanding.Upheld);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Badge.Should().Be(TourismBadge.None);
    }

    [Fact]
    public async Task An_unreviewed_report_holds_the_top_badge_back()
    {
        GivenRegisteredOperator();
        GivenIdentityAnswers(950, standing: ReportStanding.UnderReview);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value!.Badge.Should().Be(TourismBadge.Silver);
    }
}
