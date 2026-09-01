using MediatR;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Organizations.Commands;

/// <summary>
/// Brings a Platform organization into the tourism business line for the first time.
///
/// <see cref="TenantId"/> is not part of what gets stored — see the note on
/// <see cref="Tourism.Domain.Organizations.TourismOrganizationProfile"/>, which has no field
/// for it — it exists on this command purely so the caller's scope can be checked before
/// anything is created. Unlike a lookup against an existing profile, there is no stored owner
/// to be tricked into trusting a forged id here: nothing yet claims this organization for
/// tourism, so the caller has to be the one asserting under which tenant it should be
/// registered, exactly as PIMA's own EvaluationContext trusts the tenant a caller asserts.
/// </summary>
public sealed record RegisterTourismProfileCommand(
    Guid TenantId,
    Guid OrganizationId,
    TourismProfileType ProfileType,
    string CategoryCode) : IRequest<Result<CurrentBadgeResponse>>;

public sealed class RegisterTourismProfileCommandHandler(
    ITourismOrganizationProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser) : IRequestHandler<RegisterTourismProfileCommand, Result<CurrentBadgeResponse>>
{
    public async Task<Result<CurrentBadgeResponse>> Handle(
        RegisterTourismProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
            return Result<CurrentBadgeResponse>.Fail(TourismErrorCodes.InvalidInput, "TenantId is required.");

        if (!currentUser.CanActOnOrganization(request.OrganizationId, request.TenantId))
            return Result<CurrentBadgeResponse>.Fail(
                TourismErrorCodes.Forbidden, "You may not register a tourism profile for that organization.");

        var existing = await profiles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (existing is not null)
            return Result<CurrentBadgeResponse>.Fail(
                TourismErrorCodes.ProfileAlreadyExists,
                "That organization already takes part in the tourism business line.");

        TourismOrganizationProfile profile;
        try
        {
            profile = TourismOrganizationProfile.Create(
                request.OrganizationId, request.TenantId, request.ProfileType, request.CategoryCode, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result<CurrentBadgeResponse>.Fail(TourismErrorCodes.InvalidInput, ex.Message);
        }

        await profiles.AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CurrentBadgeResponse>.Ok(CurrentBadgeResponse.From(profile));
    }
}
