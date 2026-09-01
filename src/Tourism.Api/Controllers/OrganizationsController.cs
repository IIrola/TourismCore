using MediatR;
using Microsoft.AspNetCore.Mvc;
using Tourism.Api.Common;
using Tourism.Application.Badges.Commands;
using Tourism.Application.Badges.DTOs;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Commands;
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
    [HttpPost("tourism-profile")]
    [ProducesResponseType(typeof(CurrentBadgeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterTourismProfile(
        Guid organizationId, [FromBody] RegisterTourismProfileRequest request, CancellationToken cancellationToken)
        => FromResult(
            await mediator.Send(
                new RegisterTourismProfileCommand(request.TenantId, organizationId, request.ProfileType, request.CategoryCode),
                cancellationToken),
            response => CreatedAtAction(nameof(GetBadge), new { organizationId }, response));

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
                    request.AssertedPossession,
                    request.RequestedByUserId),
                cancellationToken));

    [HttpGet("badge")]
    [ProducesResponseType(typeof(CurrentBadgeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBadge(Guid organizationId, CancellationToken cancellationToken)
        => FromResult(await mediator.Send(new GetCurrentBadgeQuery(organizationId), cancellationToken));
}

/// <summary><see cref="RegisterTourismProfileCommand"/>'s body — OrganizationId comes from the route.</summary>
public sealed record RegisterTourismProfileRequest(Guid TenantId, TourismProfileType ProfileType, string CategoryCode);

/// <summary><see cref="AssessOperatorBadgeCommand"/>'s body — OrganizationId comes from the route.</summary>
public sealed record AssessOperatorBadgeRequest(
    Guid TenantId,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts,
    IReadOnlyList<AssertedPossession>? AssertedPossession = null,
    Guid? RequestedByUserId = null);
