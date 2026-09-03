using MediatR;
using Microsoft.AspNetCore.Mvc;
using Tourism.Api.Common;
using Tourism.Application.Badges.Commands;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Commands;
using Tourism.Application.Organizations.DTOs;
using Tourism.Application.Organizations.Queries;
using Tourism.Domain.Organizations;

namespace Tourism.Api.Controllers;

/// <summary>
/// BIT's public surface: bringing an organization into the tourism business line, recording
/// that it is still trading, asking PIMA to assess its identity evidence, and reading back
/// the badge that resulted. Every action requires an authenticated Platform token — see
/// Program.cs's <c>FallbackPolicy</c> — and every handler behind these routes additionally
/// checks the caller's scope against the target organization.
/// </summary>
[Route("api/v1/organizations/{organizationId:guid}")]
public sealed class OrganizationsController(IMediator mediator) : ApiControllerBase
{
    /// <summary>
    /// Onboards an organization into tourism: classifies it and decides its first badge.
    ///
    /// Requires a token scoped to this organization's tourism participation — that is the
    /// proof the organization actually joined the business line, and it is where the tenant
    /// comes from, so neither can be asserted by the request.
    /// </summary>
    [HttpPost("tourism-profile")]
    [ProducesResponseType(typeof(OnboardingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Onboard(
        Guid organizationId, [FromBody] OnboardOrganizationRequest request, CancellationToken cancellationToken)
        => FromResult(
            await mediator.Send(
                new OnboardOrganizationCommand(
                    organizationId,
                    request.ProfileType,
                    request.CategoryCode,
                    request.CorrelationId,
                    request.Contacts),
                cancellationToken),
            response => CreatedAtAction(nameof(GetBadge), new { organizationId }, response));

    /// <summary>The tourism catalogue a participant can be classified under.</summary>
    [HttpGet("/api/v1/tourism-categories")]
    [ProducesResponseType(typeof(IReadOnlyList<TourismCategory>), StatusCodes.Status200OK)]
    public IActionResult Categories([FromQuery] TourismProfileType? profileType)
        => Ok(profileType is { } type ? TourismCategories.For(type) : TourismCategories.All);

    [HttpPost("proof-of-life")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordProofOfLife(Guid organizationId, CancellationToken cancellationToken)
        => FromResult(await mediator.Send(new RecordProofOfLifeCommand(organizationId), cancellationToken));

    [HttpPost("badge-assessment")]
    [ProducesResponseType(typeof(BadgeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AssessBadge(
        Guid organizationId, [FromBody] AssessOperatorBadgeRequest request, CancellationToken cancellationToken)
        => FromResult(
            await mediator.Send(
                new AssessOperatorBadgeCommand(
                    request.TenantId,
                    organizationId,
                    request.CorrelationId,
                    request.Contacts,
                    request.RequestedByUserId),
                cancellationToken));

    [HttpGet("badge")]
    [ProducesResponseType(typeof(CurrentBadgeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBadge(Guid organizationId, CancellationToken cancellationToken)
        => FromResult(await mediator.Send(new GetCurrentBadgeQuery(organizationId), cancellationToken));
}

/// <summary>
/// <see cref="OnboardOrganizationCommand"/>'s body — the organization comes from the route, and
/// the tenant from the token rather than from either.
/// </summary>
public sealed record OnboardOrganizationRequest(
    TourismProfileType ProfileType,
    string CategoryCode,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts);

/// <summary><see cref="AssessOperatorBadgeCommand"/>'s body — OrganizationId comes from the route.</summary>
/// <remarks>
/// No possession field. It used to be one, and a caller filling it in awarded themselves the
/// heaviest input to an identity score; BIT now asks Platform instead.
/// </remarks>
public sealed record AssessOperatorBadgeRequest(
    Guid TenantId,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts,
    Guid? RequestedByUserId = null);
