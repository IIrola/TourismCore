using MediatR;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Common;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;

namespace Tourism.Application.Badges.Commands;

/// <summary>
/// Decides the badge a tourism operator may display, asking the identity engine for the facts
/// and applying tourism's own judgement to them.
///
/// This is the flow that crosses all three services: Platform authorized the caller and
/// issued the service token, PIMA states what the evidence supports, and BIT decides what
/// that means for a listing. Each answers only what it owns.
/// </summary>
public sealed record AssessOperatorBadgeCommand(
    Guid TenantId,
    Guid OrganizationId,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts,
    IReadOnlyList<AssertedPossession>? AssertedPossession = null,
    Guid? RequestedByUserId = null) : IRequest<Result<BadgeResponse>>;

public sealed class AssessOperatorBadgeCommandHandler(
    ITourismOrganizationProfileRepository profiles,
    IIdentityEvaluationClient identityClient,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser) : IRequestHandler<AssessOperatorBadgeCommand, Result<BadgeResponse>>
{
    public async Task<Result<BadgeResponse>> Handle(
        AssessOperatorBadgeCommand request, CancellationToken cancellationToken)
    {
        if (request.Contacts.Count == 0)
            return Result<BadgeResponse>.Fail(
                TourismErrorCodes.InvalidInput, "At least one contact is required to assess a badge.");

        var profile = await profiles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (profile is null)
            return Result<BadgeResponse>.Fail(
                TourismErrorCodes.ProfileNotFound,
                "That organization does not take part in the tourism business line.");

        // Deliberately NOT request.TenantId: that is only the context BIT forwards to PIMA
        // for tracing/billing, and trusting it here would let a caller claim authority over
        // someone else's organization simply by asserting their own tenant id. See
        // ScopeAuthorization for why the real check (against the profile's owning tenant)
        // cannot be done yet.
        if (!ScopeAuthorization.CanActOn(currentUser, profile))
            return Result<BadgeResponse>.Fail(
                TourismErrorCodes.Forbidden, "You may not assess a badge for that organization.");

        var evaluation = await identityClient.EvaluateAsync(
            new IdentityEvaluationRequest(
                request.TenantId,
                request.OrganizationId,
                request.CorrelationId,
                request.Contacts,
                request.AssertedPossession,
                RequestedByUserId: request.RequestedByUserId),
            cancellationToken);

        if (!evaluation.IsSuccess)
        {
            // The engine being unreachable is not a tourism verdict, and BIT must not invent
            // one. The previously recorded badge stays exactly as it was — silently downgrading
            // an operator because a dependency was down would publish a claim about them that
            // nothing supports.
            //
            // Whether a listing should eventually expire its badge after a long outage is a
            // business decision that has not been taken; until it is, nothing here decays it.
            return Result<BadgeResponse>.Fail(
                TourismErrorCodes.IdentityServiceUnavailable,
                evaluation.ErrorMessage ?? "The identity engine could not be reached.");
        }

        var outcome = evaluation.Value!;
        var now = clock.UtcNow;
        var decision = BadgeAssessment.Decide(outcome.Assessment, profile, now);

        profile.RecordBadge(decision, outcome.EvaluationId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BadgeResponse>.Ok(BadgeResponse.From(profile, decision, outcome));
    }
}
