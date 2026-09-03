using Tourism.Application.Badges.DTOs;

namespace Tourism.Application.Organizations.DTOs;

/// <summary>
/// What onboarding produced: the tourism listing, and the badge it earned on the spot.
///
/// <paramref name="Badge"/> is null when no assessment could be made — no contacts to evaluate,
/// or the identity engine was unreachable — and <paramref name="Note"/> then says which. The
/// two are kept apart deliberately: an operator with no badge yet and an operator whose
/// evidence failed are different situations, and collapsing them into one empty field is what
/// left the legacy unable to tell an unchecked listing from a rejected one.
/// </summary>
public sealed record OnboardingResponse(
    CurrentBadgeResponse Profile,
    BadgeResponse? Badge,
    string? Note);
