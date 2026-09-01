using MediatR;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Common;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Common;

namespace Tourism.Application.Organizations.Queries;

/// <summary>Reads the badge as currently recorded, without asking the identity engine again.</summary>
public sealed record GetCurrentBadgeQuery(Guid OrganizationId) : IRequest<Result<CurrentBadgeResponse>>;

public sealed class GetCurrentBadgeQueryHandler(
    ITourismOrganizationProfileRepository profiles,
    ICurrentUser currentUser) : IRequestHandler<GetCurrentBadgeQuery, Result<CurrentBadgeResponse>>
{
    public async Task<Result<CurrentBadgeResponse>> Handle(
        GetCurrentBadgeQuery request, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (profile is null)
            return Result<CurrentBadgeResponse>.Fail(
                TourismErrorCodes.ProfileNotFound,
                "That organization does not take part in the tourism business line.");

        if (!ScopeAuthorization.CanActOn(currentUser, profile))
            return Result<CurrentBadgeResponse>.Fail(TourismErrorCodes.Forbidden, "You may not view that organization.");

        return Result<CurrentBadgeResponse>.Ok(CurrentBadgeResponse.From(profile));
    }
}
