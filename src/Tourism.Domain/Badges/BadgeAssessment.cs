using Tourism.Domain.Organizations;

namespace Tourism.Domain.Badges;

/// <summary>The badge awarded, and why — in terms an operator can be told.</summary>
public sealed record BadgeDecision(TourismBadge Badge, IReadOnlyList<string> Reasons);

/// <summary>
/// Decides which badge a tourism operator may display.
///
/// This is the line between the two services. PIMA states what the evidence supports and
/// stops there; deciding what that means for a tourism listing is a business judgement, and
/// it belongs to the vertical that has to defend it. In the legacy the two were fused inside
/// the scoring service, which is why the engine could never be reused: it did not just
/// measure identity, it decided a tourism badge and capped it by subscription tier.
///
/// A pure function, like the scoring rule it consumes and the permission rule in Platform —
/// every input passed in, no storage touched, so the rule that decides what the public sees
/// can be tested exhaustively.
/// </summary>
public static class BadgeAssessment
{
    /// <summary>Score at or above which the evidence supports the top evidence-based badge.</summary>
    public const int GoldScoreThreshold = 800;
    public const int SilverScoreThreshold = 600;
    public const int BronzeScoreThreshold = 400;

    /// <summary>
    /// Evidence breadth required for the higher badges. A near-perfect score drawn from one
    /// signal out of five is not the same claim as the same score drawn from all five, and
    /// the legacy could not tell them apart because it never published coverage.
    /// </summary>
    public const decimal GoldCoverageThreshold = 0.75m;
    public const decimal SilverCoverageThreshold = 0.50m;

    /// <summary>
    /// How long an operator may go without showing signs of trading before the badge is held
    /// back. Recovered from the legacy's proof-of-life concept: in tourism a listing that has
    /// gone quiet is its own kind of risk, whatever the identity behind it once verified.
    /// </summary>
    public static readonly TimeSpan ProofOfLifeWindow = TimeSpan.FromDays(365);

    public static BadgeDecision Decide(
        IdentityAssessment assessment,
        TourismOrganizationProfile profile,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(profile);

        var reasons = new List<string>();

        // Nothing conclusive means no claim, not the worst claim. Publishing an unchecked
        // operator as a failed one would be a statement the evidence does not support.
        if (!assessment.IsConclusive)
        {
            reasons.Add("The identity evaluation was inconclusive: no evidence was gathered.");
            return new BadgeDecision(TourismBadge.Undetermined, reasons);
        }

        var score = assessment.Score!.Value;
        var badge = FromScore(score, reasons);

        if (badge == TourismBadge.None)
            return new BadgeDecision(badge, reasons);

        badge = CapByCoverage(badge, assessment.Coverage, reasons);
        badge = CapByProofOfLife(badge, profile, nowUtc, reasons);

        return new BadgeDecision(badge, reasons);
    }

    private static TourismBadge FromScore(int score, List<string> reasons)
    {
        if (score >= GoldScoreThreshold)
        {
            reasons.Add($"Identity score of {score} meets the Gold threshold of {GoldScoreThreshold}.");
            return TourismBadge.Gold;
        }

        if (score >= SilverScoreThreshold)
        {
            reasons.Add($"Identity score of {score} meets the Silver threshold of {SilverScoreThreshold}.");
            return TourismBadge.Silver;
        }

        if (score >= BronzeScoreThreshold)
        {
            reasons.Add($"Identity score of {score} meets the Bronze threshold of {BronzeScoreThreshold}.");
            return TourismBadge.Bronze;
        }

        reasons.Add($"Identity score of {score} is below the Bronze threshold of {BronzeScoreThreshold}.");
        return TourismBadge.None;
    }

    private static TourismBadge CapByCoverage(TourismBadge badge, decimal coverage, List<string> reasons)
    {
        if (badge >= TourismBadge.Gold && coverage < GoldCoverageThreshold)
        {
            reasons.Add(
                $"Held at Silver: only {coverage:P0} of the identity checks produced evidence, " +
                $"and Gold requires {GoldCoverageThreshold:P0}.");
            badge = TourismBadge.Silver;
        }

        if (badge >= TourismBadge.Silver && coverage < SilverCoverageThreshold)
        {
            reasons.Add(
                $"Held at Bronze: only {coverage:P0} of the identity checks produced evidence, " +
                $"and Silver requires {SilverCoverageThreshold:P0}.");
            badge = TourismBadge.Bronze;
        }

        return badge;
    }

    private static TourismBadge CapByProofOfLife(
        TourismBadge badge, TourismOrganizationProfile profile, DateTime nowUtc, List<string> reasons)
    {
        if (badge <= TourismBadge.Bronze)
            return badge;

        var elapsed = profile.TimeSinceProofOfLife(nowUtc);

        if (elapsed is null)
        {
            reasons.Add("Held at Bronze: the operator has never shown signs of trading.");
            return TourismBadge.Bronze;
        }

        if (elapsed > ProofOfLifeWindow)
        {
            reasons.Add(
                $"Held at Bronze: the operator has not shown signs of trading in " +
                $"{elapsed.Value.Days} days, past the {ProofOfLifeWindow.Days}-day window.");
            return TourismBadge.Bronze;
        }

        return badge;
    }
}
