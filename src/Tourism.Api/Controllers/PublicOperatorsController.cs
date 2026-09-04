using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tourism.Api.Common;
using Tourism.Application.PublicDirectory.Ports;
using Tourism.Application.PublicDirectory.Queries;

namespace Tourism.Api.Controllers;

/// <summary>
/// The public tourism directory: anonymous, and the only anonymous surface in this migration
/// that is supposed to be one.
///
/// Where the legacy served this from the engine — assembling identity, risk facts and tourism
/// business data into one response, on an anonymous route, addressed by an enumerable
/// identifier — the page is composed here and the facts come from PIMA under the subject's own
/// consent. Three consequences follow, and all three are the point:
///
/// The engine has no anonymous surface. Nothing about scoring, evidence or incident reports is
/// reachable without a service token.
///
/// A vertical can publish its own fields without touching the engine. The tourism category
/// label lives here, where "Guías de Turistas" belongs.
///
/// And an operator who withdrew their consent has no page, whatever this service still holds
/// about them, because the identity facts are asked for first and a refusal ends the request.
/// </summary>
[Route("api/v1/public/operators")]
[AllowAnonymous]
public sealed class PublicOperatorsController(IMediator mediator) : ApiControllerBase
{
    /// <summary>
    /// The public page for an operator.
    ///
    /// A subject who withdrew and one who never existed answer identically: an old link must
    /// not still confirm that somebody is listed after they asked not to be.
    /// </summary>
    [HttpGet("{publicDirectoryId}")]
    [ProducesResponseType(typeof(PublicOperatorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(string publicDirectoryId, CancellationToken cancellationToken)
        => FromResult(await mediator.Send(new GetPublicOperatorQuery(publicDirectoryId), cancellationToken));

    /// <summary>
    /// Finds an operator by one of their contacts, if its owner agreed to being findable.
    ///
    /// A POST on an anonymous route, deliberately: the contact is personal data and does not
    /// belong in a URL, a proxy log or a browser history. Its predecessor was
    /// <c>GET ?contact=</c> — and worse, that handler called a paid provider, created an
    /// identity profile and emailed an account-activation link to whatever address had been
    /// typed into the search box.
    /// </summary>
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(PublicOperatorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Lookup(
        [FromBody] LookupBody body, CancellationToken cancellationToken)
        => FromResult(await mediator.Send(
            new LookupPublicOperatorQuery(body.Channel, body.Value), cancellationToken));

    public sealed record LookupBody(DirectoryChannel Channel, string Value);
}
