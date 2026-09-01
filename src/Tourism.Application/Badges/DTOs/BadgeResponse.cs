using Tourism.Application.Identity.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Badges.DTOs;

/// <summary>
/// The badge decision, with everything needed to explain it.
///
/// <see cref="EvaluationId"/> points at the identity engine's own record of the evidence, so a
/// disputed badge can be traced all the way back to what the providers actually said rather
/// than stopping at "the system decided".
/// </summary>
public sealed record BadgeResponse(
    Guid OrganizationId,
    TourismBadge Badge,
    IReadOnlyList<string> Reasons,
    DateTime AssessedAtUtc,
    Guid EvaluationId,
    int? IdentityScore,
    decimal IdentityCoverage)
{
    public static BadgeResponse From(
        TourismOrganizationProfile profile, BadgeDecision decision, IdentityEvaluationOutcome outcome) =>
        new(
            profile.OrganizationId,
            decision.Badge,
            decision.Reasons,
            profile.BadgeAssessedAtUtc!.Value,
            outcome.EvaluationId,
            outcome.Assessment.Score,
            outcome.Assessment.Coverage);
}

/// <summary>The badge as currently recorded, for rendering a directory listing.</summary>
public sealed record CurrentBadgeResponse(
    Guid OrganizationId,
    TourismProfileType ProfileType,
    string CategoryCode,
    TourismBadge Badge,
    DateTime? AssessedAtUtc,
    string? Reasons,
    Guid? EvaluationId)
{
    public static CurrentBadgeResponse From(TourismOrganizationProfile profile) =>
        new(
            profile.OrganizationId,
            profile.ProfileType,
            profile.CategoryCode,
            // Never assessed reads as undetermined, not as a failed assessment.
            profile.CurrentBadge ?? TourismBadge.Undetermined,
            profile.BadgeAssessedAtUtc,
            profile.BadgeReasons,
            profile.LastEvaluationId);
}
