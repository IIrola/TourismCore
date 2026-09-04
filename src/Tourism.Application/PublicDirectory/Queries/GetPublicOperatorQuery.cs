using MediatR;
using Tourism.Application.Organizations.Ports;
using Tourism.Application.PublicDirectory.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.PublicDirectory.Queries;

/// <summary>
/// The tourism half of a public listing: what BIT itself says about an operator.
/// </summary>
/// <param name="CategoryLabel">The tourism category, in tourism's words. This is the field
/// that made the legacy's public profile impossible to decompose: its equivalent lived on the
/// platform's own user record, and its catalogue was seeded exclusively with tourism trades.
/// A platform meant to be vertical-agnostic cannot carry "Guías de Turistas" as a column.</param>
public sealed record PublicOperatorResponse(
    string PublicDirectoryId,
    TourismProfileType ProfileType,
    string CategoryCode,
    string? CategoryLabel,
    TourismBadge? Badge,
    DateTime? BadgeAssessedAtUtc,
    DateTime RegisteredAtUtc,
    DateTime? LastProofOfLifeAtUtc,
    PublishedIdentity Identity);

/// <summary>
/// Serves the public page for an operator, composing what BIT knows with what the identity
/// engine agrees may be published.
///
/// This is the composition the legacy did inside the engine, moved to where it belongs. The
/// order of the two calls matters: <b>the identity facts are asked for first, and a page
/// exists only if the engine says something may be published.</b> An operator who withdrew
/// their consent has no public page, whatever BIT still holds about them — which is the whole
/// point of the consent being the engine's to enforce rather than each vertical's to remember.
/// </summary>
public sealed record GetPublicOperatorQuery(string PublicDirectoryId) : IRequest<Result<PublicOperatorResponse>>;

public sealed class GetPublicOperatorQueryHandler(
    ITourismOrganizationProfileRepository profiles,
    IPublicDirectoryClient directory) : IRequestHandler<GetPublicOperatorQuery, Result<PublicOperatorResponse>>
{
    public async Task<Result<PublicOperatorResponse>> Handle(
        GetPublicOperatorQuery request, CancellationToken cancellationToken)
    {
        var identity = await directory.GetPublishedAsync(request.PublicDirectoryId, cancellationToken);

        if (!identity.IsSuccess)
        {
            // Forwarded rather than reinterpreted. A not-found from the engine means "nothing
            // may be published about this identifier", and a page assembled anyway from BIT's
            // own data would publish an operator who had asked not to be listed.
            return Result<PublicOperatorResponse>.Fail(identity.ErrorCode!, identity.ErrorMessage);
        }

        var profile = await profiles.GetByPublicDirectoryIdAsync(request.PublicDirectoryId, cancellationToken);
        if (profile is null)
        {
            // The engine publishes this subject, but no tourism operator is that subject. A
            // person can be in the directory through another vertical entirely, and BIT has
            // no page for them.
            return Result<PublicOperatorResponse>.Fail(TourismErrorCodes.ProfileNotFound);
        }

        return Result<PublicOperatorResponse>.Ok(new PublicOperatorResponse(
            request.PublicDirectoryId,
            profile.ProfileType,
            profile.CategoryCode,
            TourismCategories.Find(profile.CategoryCode)?.Name,
            profile.CurrentBadge,
            profile.BadgeAssessedAtUtc,
            profile.RegisteredAtUtc,
            profile.LastProofOfLifeAtUtc,
            identity.Value!));
    }
}

/// <summary>
/// Finds the public page for a contact, if its owner agreed to being findable.
///
/// The whole answer comes from the engine. BIT adds nothing to the question and must not: it
/// has no way of knowing whether somebody consented to being found, and guessing would make
/// this the third place in the codebase that decides who is discoverable.
/// </summary>
public sealed record LookupPublicOperatorQuery(DirectoryChannel Channel, string Value)
    : IRequest<Result<PublicOperatorResponse>>;

public sealed class LookupPublicOperatorQueryHandler(IMediator mediator, IPublicDirectoryClient directory)
    : IRequestHandler<LookupPublicOperatorQuery, Result<PublicOperatorResponse>>
{
    public async Task<Result<PublicOperatorResponse>> Handle(
        LookupPublicOperatorQuery request, CancellationToken cancellationToken)
    {
        var found = await directory.LookupAsync(request.Channel, request.Value, cancellationToken);

        if (!found.IsSuccess)
            return Result<PublicOperatorResponse>.Fail(found.ErrorCode!, found.ErrorMessage);

        // One shape of answer for every negative: nobody holds that contact, they are not
        // listed, they are not findable, or that particular contact was withheld. Any
        // distinction between them is a way of confirming a contact belongs to somebody.
        if (string.IsNullOrEmpty(found.Value))
            return Result<PublicOperatorResponse>.Fail(TourismErrorCodes.NotFound);

        return await mediator.Send(new GetPublicOperatorQuery(found.Value), cancellationToken);
    }
}
