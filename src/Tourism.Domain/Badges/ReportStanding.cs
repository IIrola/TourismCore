namespace Tourism.Domain.Badges;

/// <summary>
/// What the incident reports against an operator's contacts amount to, in BIT's own words.
///
/// BIT's own enum, like <see cref="IdentityAssessment"/> and for the same reason: the two
/// services share the shape of an answer, never an assembly.
///
/// The middle value is the one that matters. PIMA reports an unreviewed accusation as exactly
/// that, instead of as an established fact, and it is BIT that decides what an unresolved
/// claim should cost an operator — which is a judgement about tourism listings, not about
/// identity evidence.
/// </summary>
public enum ReportStanding
{
    None = 0,
    UnderReview = 1,
    Upheld = 2
}
