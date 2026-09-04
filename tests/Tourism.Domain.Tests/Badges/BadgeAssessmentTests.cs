using FluentAssertions;
using Tourism.Domain.Badges;
using Tourism.Domain.Organizations;
using Xunit;

namespace Tourism.Domain.Tests.Badges;

/// <summary>
/// The rule that decides what the public sees next to a tourism operator.
///
/// This is the line between the two services: PIMA states what the evidence supports, BIT
/// decides what that means for a listing. Tested exhaustively because it is pure — and
/// because in the legacy this judgement lived inside the scoring engine, where it could not
/// be examined on its own.
/// </summary>
public class BadgeAssessmentTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private static TourismOrganizationProfile Operator(DateTime? lastProofOfLife = null)
    {
        var profile = TourismOrganizationProfile.Create(
            Guid.NewGuid(), Guid.NewGuid(), TourismProfileType.Operator, "tour-guide", Now.AddYears(-3));
        if (lastProofOfLife.HasValue)
            profile.RecordProofOfLife(lastProofOfLife.Value);
        return profile;
    }

    private static IdentityAssessment Assessment(int? score, decimal coverage = 1m)
        => new(score, coverage, Now);

    private static BadgeDecision Decide(
        int? score,
        decimal coverage = 1m,
        DateTime? proofOfLife = null,
        ReportStanding standing = ReportStanding.None)
        => BadgeAssessment.Decide(
            Assessment(score, coverage), Operator(proofOfLife ?? Now.AddDays(-30)), standing, Now);

    // ---------- the distinction the legacy could not make ----------

    [Fact]
    public void An_inconclusive_evaluation_yields_no_claim_rather_than_a_failed_one()
    {
        // The legacy published an unchecked operator exactly like one that had been checked
        // and failed. Those are different statements.
        var decision = Decide(score: null);

        decision.Badge.Should().Be(TourismBadge.Undetermined);
        decision.Reasons.Should().ContainSingle(r => r.Contains("inconclusive"));
    }

    [Fact]
    public void A_conclusive_bad_score_yields_None_which_is_not_Undetermined()
    {
        var checkedAndFailed = Decide(score: 100);
        var neverChecked = Decide(score: null);

        checkedAndFailed.Badge.Should().Be(TourismBadge.None);
        neverChecked.Badge.Should().Be(TourismBadge.Undetermined);
        checkedAndFailed.Badge.Should().NotBe(neverChecked.Badge);
    }

    // ---------- score bands ----------

    [Theory]
    [InlineData(1000, TourismBadge.Gold)]
    [InlineData(800, TourismBadge.Gold)]
    [InlineData(799, TourismBadge.Silver)]
    [InlineData(600, TourismBadge.Silver)]
    [InlineData(599, TourismBadge.Bronze)]
    [InlineData(400, TourismBadge.Bronze)]
    [InlineData(399, TourismBadge.None)]
    [InlineData(0, TourismBadge.None)]
    public void Full_evidence_and_recent_trading_map_the_score_onto_a_badge(int score, TourismBadge expected)
    {
        Decide(score).Badge.Should().Be(expected);
    }

    // ---------- coverage ----------

    [Fact]
    public void A_high_score_on_thin_evidence_does_not_earn_Gold()
    {
        // A near-perfect score drawn from one signal out of five is not the same claim as the
        // same score drawn from all five. The legacy could not tell them apart.
        var thorough = Decide(900, coverage: 1m);
        var thin = Decide(900, coverage: 0.20m);

        thorough.Badge.Should().Be(TourismBadge.Gold);
        thin.Badge.Should().Be(TourismBadge.Bronze);
        thin.Reasons.Should().Contain(r => r.Contains("20%"));
    }

    [Theory]
    [InlineData(1.00, TourismBadge.Gold)]
    [InlineData(0.75, TourismBadge.Gold)]
    [InlineData(0.74, TourismBadge.Silver)]
    [InlineData(0.50, TourismBadge.Silver)]
    [InlineData(0.49, TourismBadge.Bronze)]
    public void Coverage_caps_how_high_a_badge_can_reach(double coverage, TourismBadge expected)
    {
        Decide(900, coverage: (decimal)coverage).Badge.Should().Be(expected);
    }

    [Fact]
    public void Coverage_never_promotes_a_badge()
    {
        // Full coverage over a mediocre score is still a mediocre score.
        Decide(450, coverage: 1m).Badge.Should().Be(TourismBadge.Bronze);
    }

    // ---------- proof of life ----------

    [Fact]
    public void An_operator_who_has_never_traded_is_held_at_Bronze()
    {
        var decision = BadgeAssessment.Decide(Assessment(900), Operator(lastProofOfLife: null), ReportStanding.None, Now);

        decision.Badge.Should().Be(TourismBadge.Bronze);
        decision.Reasons.Should().Contain(r => r.Contains("never shown signs of trading"));
    }

    [Fact]
    public void An_operator_gone_quiet_past_the_window_is_held_at_Bronze()
    {
        // A travel agency that verified perfectly two years ago and has not been seen since
        // is not the same proposition as one seen last week.
        var stale = Decide(900, proofOfLife: Now - BadgeAssessment.ProofOfLifeWindow.Add(TimeSpan.FromDays(1)));

        stale.Badge.Should().Be(TourismBadge.Bronze);
        stale.Reasons.Should().Contain(r => r.Contains("not shown signs of trading"));
    }

    [Fact]
    public void Trading_within_the_window_does_not_hold_the_badge_back()
    {
        Decide(900, proofOfLife: Now - BadgeAssessment.ProofOfLifeWindow.Add(TimeSpan.FromDays(-1)))
            .Badge.Should().Be(TourismBadge.Gold);
    }

    [Fact]
    public void Proof_of_life_does_not_hold_back_a_badge_already_at_Bronze()
    {
        // There is nothing left to hold back, and saying so would only add noise.
        var decision = BadgeAssessment.Decide(Assessment(450), Operator(lastProofOfLife: null), ReportStanding.None, Now);

        decision.Badge.Should().Be(TourismBadge.Bronze);
        decision.Reasons.Should().NotContain(r => r.Contains("trading"));
    }

    // ---------- explainability ----------

    [Fact]
    public void Every_decision_explains_itself()
    {
        // A business decision that cannot say why is the legacy's problem repeated: its shield
        // was a number on a row with no record of how it got there.
        foreach (var decision in new[] { Decide(900), Decide(100), Decide(null), Decide(900, 0.1m) })
            decision.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void A_held_badge_says_both_what_it_qualified_for_and_why_it_was_held()
    {
        var decision = Decide(900, coverage: 0.20m);

        decision.Reasons.Should().Contain(r => r.Contains("Gold threshold"));
        decision.Reasons.Should().Contain(r => r.Contains("Held at"));
    }

    // ---------- what is deliberately absent ----------

    [Fact]
    public void No_amount_of_evidence_awards_Platinum()
    {
        // Platinum was the legacy's plan-gated tier. Whether a commercial tier may raise a
        // badge is still an open business decision, so nothing here awards it.
        BadgeAssessment.Decide(Assessment(1000, 1m), Operator(Now), ReportStanding.None, Now)
            .Badge.Should().Be(TourismBadge.Gold);
    }

    // ---------- incident reports ----------

    [Fact]
    public void An_upheld_report_leaves_an_operator_with_no_badge_whatever_they_scored()
    {
        var decision = Decide(1000, coverage: 1m, standing: ReportStanding.Upheld);

        decision.Badge.Should().Be(TourismBadge.None);
        decision.Reasons.Should().Contain(r => r.Contains("upheld incident report"));
    }

    [Fact]
    public void An_unreviewed_report_holds_the_top_badge_back_without_taking_one_away()
    {
        // A claim nobody has decided is grounds not to make the strongest public statement
        // yet — not grounds to strip a badge. The legacy did the opposite: an unreviewed
        // report zeroed the score platform-wide the moment it was filed, so anyone with a
        // reporting account could cost a competitor their standing.
        var decision = Decide(1000, coverage: 1m, standing: ReportStanding.UnderReview);

        decision.Badge.Should().Be(TourismBadge.Silver);
        decision.Reasons.Should().Contain(r => r.Contains("awaiting review"));
    }

    [Fact]
    public void An_unreviewed_report_does_not_disturb_a_badge_below_the_top()
    {
        var decision = Decide(650, coverage: 1m, standing: ReportStanding.UnderReview);

        decision.Badge.Should().Be(TourismBadge.Silver);
        decision.Reasons.Should().NotContain(r => r.Contains("awaiting review"));
    }

    [Fact]
    public void An_upheld_report_is_decided_before_the_score_is_even_read()
    {
        // Including when the evaluation was inconclusive: the report is a fact about conduct,
        // and it does not need the identity evidence to say anything.
        Decide(null, standing: ReportStanding.Upheld).Badge.Should().Be(TourismBadge.None);
    }
}
