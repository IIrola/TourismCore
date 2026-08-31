namespace Tourism.Domain.Badges;

/// <summary>
/// What the identity engine concluded, in BIT's own words.
///
/// Deliberately BIT's own type rather than one imported from PIMA. The two services share no
/// code: BIT depends on the shape of an answer, not on the assembly that produced it, and a
/// change to PIMA's internals must not be able to break a build here.
///
/// <paramref name="Score"/> is nullable because PIMA distinguishes "we found nothing" from
/// "this is as risky as it gets", and BIT has to preserve that distinction rather than
/// collapse it into a number.
/// </summary>
/// <param name="Score">0-1000 where higher is more trust, or null when inconclusive.</param>
/// <param name="Coverage">
/// Share of the scoring profile actually backed by evidence, 0 to 1. A high score over thin
/// coverage is a weaker claim than the same score over full coverage, and the badge rules
/// below act on that difference.
/// </param>
public sealed record IdentityAssessment(int? Score, decimal Coverage, DateTime EvaluatedAtUtc)
{
    public bool IsConclusive => Score.HasValue;
}
