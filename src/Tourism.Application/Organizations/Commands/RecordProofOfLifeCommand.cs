using MediatR;
using Tourism.Application.Common;
using Tourism.Application.Common.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Domain.Common;

namespace Tourism.Application.Organizations.Commands;

/// <summary>
/// Marks that a tourism operator has just done something that proves it is still trading.
/// See <see cref="Tourism.Domain.Organizations.TourismOrganizationProfile.RecordProofOfLife"/>
/// for why the badge rules care about this independently of identity evidence.
/// </summary>
public sealed record RecordProofOfLifeCommand(Guid OrganizationId) : IRequest<Result>;

public sealed class RecordProofOfLifeCommandHandler(
    ITourismOrganizationProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser) : IRequestHandler<RecordProofOfLifeCommand, Result>
{
    public async Task<Result> Handle(RecordProofOfLifeCommand request, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByOrganizationAsync(request.OrganizationId, cancellationToken);
        if (profile is null)
            return Result.Fail(
                TourismErrorCodes.ProfileNotFound,
                "That organization does not take part in the tourism business line.");

        if (!ScopeAuthorization.CanActOn(currentUser, profile))
            return Result.Fail(TourismErrorCodes.Forbidden, "You may not act on that organization.");

        profile.RecordProofOfLife(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok;
    }
}
