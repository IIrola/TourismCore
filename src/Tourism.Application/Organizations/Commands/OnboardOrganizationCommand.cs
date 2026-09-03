using MediatR;
using Tourism.Application.Badges.Commands;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.DTOs;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Organizations.Commands;

/// <summary>
/// Brings a Platform organization into the tourism business line and decides, there and then,
/// what it may show.
///
/// This is BIT's half of onboarding. Platform has already created the person, the tenant, the
/// organization and the participation; PIMA states what the evidence about the operator
/// supports. What is left is the tourism judgement — is this a classification we recognize,
/// and what badge does the evidence earn — and that is the only part BIT owns.
///
/// The tenant and the organization are taken from the caller's token, never from the request.
/// An earlier version accepted a tenant id in the body because there was no stored owner yet
/// to check against; a participation-scoped token removes that gap, since Platform will not
/// mint one for a withdrawn participation, an archived organization or a suspended tenant. The
/// request still names the organization so a mismatch is refused out loud rather than silently
/// resolved to whatever the token happened to say.
/// </summary>
public sealed record OnboardOrganizationCommand(
    Guid OrganizationId,
    TourismProfileType ProfileType,
    string CategoryCode,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts,
    IReadOnlyList<AssertedPossession>? AssertedPossession = null) : IRequest<Result<OnboardingResponse>>;

public sealed class OnboardOrganizationCommandHandler(
    ITourismOrganizationProfileRepository profiles,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser) : IRequestHandler<OnboardOrganizationCommand, Result<OnboardingResponse>>
{
    public async Task<Result<OnboardingResponse>> Handle(
        OnboardOrganizationCommand request, CancellationToken cancellationToken)
    {
        // The proof that this organization actually joined tourism. Without it BIT would be
        // creating a tourism profile for a company that never entered the business line —
        // exactly the conflation between "has an account" and "trades in tourism" that the
        // legacy could not express at all.
        if (!currentUser.ActsInTourismFor(request.OrganizationId))
        {
            return Result<OnboardingResponse>.Fail(
                TourismErrorCodes.Forbidden,
                "Onboarding requires a token scoped to this organization's tourism participation.");
        }

        if (currentUser.TenantId is not { } tenantId)
        {
            return Result<OnboardingResponse>.Fail(
                TourismErrorCodes.Forbidden, "The caller's token does not name an owning tenant.");
        }

        if (!TourismCategories.Permits(request.ProfileType, request.CategoryCode))
        {
            return Result<OnboardingResponse>.Fail(
                TourismErrorCodes.InvalidInput,
                $"'{request.CategoryCode}' is not a tourism category a {request.ProfileType} can be classified under.");
        }

        var existing = await profiles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (existing is not null)
        {
            return Result<OnboardingResponse>.Fail(
                TourismErrorCodes.ProfileAlreadyExists,
                "That organization already takes part in the tourism business line.");
        }

        TourismOrganizationProfile profile;
        try
        {
            profile = TourismOrganizationProfile.Create(
                request.OrganizationId, tenantId, request.ProfileType, request.CategoryCode, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result<OnboardingResponse>.Fail(TourismErrorCodes.InvalidInput, ex.Message);
        }

        await profiles.AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var badge = await AssessOnceRegisteredAsync(request, tenantId, cancellationToken);

        return Result<OnboardingResponse>.Ok(
            new OnboardingResponse(CurrentBadgeResponse.From(profile), badge.Value, badge.Note));
    }

    /// <summary>
    /// Runs the first badge assessment, reusing the one implementation that owns it rather
    /// than repeating the orchestration here.
    ///
    /// A failure is reported, not propagated. The organization has joined tourism either way,
    /// and refusing the whole onboarding because the identity engine was briefly unreachable
    /// would undo work that already succeeded and is not the engine's to undo. The listing
    /// simply has no badge yet, which is what an unassessed operator should look like.
    /// </summary>
    private async Task<(BadgeResponse? Value, string? Note)> AssessOnceRegisteredAsync(
        OnboardOrganizationCommand request, Guid tenantId, CancellationToken cancellationToken)
    {
        if (request.Contacts.Count == 0)
            return (null, "No contacts were supplied, so no identity evidence could be gathered yet.");

        var result = await mediator.Send(
            new AssessOperatorBadgeCommand(
                tenantId,
                request.OrganizationId,
                request.CorrelationId,
                request.Contacts,
                request.AssertedPossession,
                currentUser.UserId == Guid.Empty ? null : currentUser.UserId),
            cancellationToken);

        return result.IsSuccess
            ? (result.Value, null)
            : (null, $"The first assessment could not be completed ({result.ErrorCode}); the badge is still pending.");
    }
}
