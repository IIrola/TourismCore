using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;

namespace Tourism.Infrastructure.Identity;

/// <summary>
/// BIT's HTTP boundary with PIMA. There is no project reference to PIMA anywhere in this
/// solution — see <see cref="IIdentityEvaluationClient"/> for why — so every shape on this
/// wire is declared again here, independently, in BIT's own words.
///
/// Every failure this method can produce — a bad status, a timeout, a connection refused, a
/// response that does not parse — comes back as
/// <see cref="TourismErrorCodes.IdentityServiceUnavailable"/> rather than an exception. The
/// handler that called this is the one that gets to decide what a tourism listing does when
/// the identity engine is unreachable, and it cannot do that from inside a catch block.
/// </summary>
public sealed class HttpIdentityEvaluationClient(
    HttpClient httpClient,
    IServiceTokenProvider tokenProvider,
    IOptions<PimaOptions> options,
    IClock clock,
    ILogger<HttpIdentityEvaluationClient> logger) : IIdentityEvaluationClient
{
    private const string EvaluationsPath = "api/v1/evaluations";

    /// <summary>Matches PIMA's own ASP.NET Core MVC default (camelCase, case-insensitive).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PimaOptions _options = options.Value;

    public async Task<Result<IdentityEvaluationOutcome>> EvaluateAsync(
        IdentityEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var token = await tokenProvider.GetTokenAsync(_options.Audience, cancellationToken);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, EvaluationsPath)
            {
                Content = JsonContent.Create(ToWireRequest(request), options: JsonOptions)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "PIMA answered with status {StatusCode} for evaluation correlation {CorrelationId}.",
                    (int)response.StatusCode, request.CorrelationId);

                return Result<IdentityEvaluationOutcome>.Fail(
                    TourismErrorCodes.IdentityServiceUnavailable,
                    $"The identity engine answered with status {(int)response.StatusCode}.");
            }

            var wireResponse = await response.Content.ReadFromJsonAsync<PimaEvaluationResponse>(
                JsonOptions, cancellationToken);

            if (wireResponse is null)
            {
                return Result<IdentityEvaluationOutcome>.Fail(
                    TourismErrorCodes.IdentityServiceUnavailable, "The identity engine returned an empty response.");
            }

            // score.value arriving null is PIMA stating "inconclusive", not an error — that
            // distinction is preserved end to end by leaving it null here rather than
            // collapsing it into 0, which would instead say "this is as risky as it gets".
            var assessment = new IdentityAssessment(
                wireResponse.Score.Value, wireResponse.Score.Coverage, clock.UtcNow);

            var standing = Enum.IsDefined(typeof(ReportStanding), wireResponse.ReportStanding)
                ? (ReportStanding)wireResponse.ReportStanding
                : ReportStanding.None;

            return Result<IdentityEvaluationOutcome>.Ok(
                new IdentityEvaluationOutcome(
                    wireResponse.Id, assessment, standing, wireResponse.PublicDirectoryId));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // HttpRequestException covers transport/connection failures; TaskCanceledException
            // covers HttpClient's own timeout (it does not throw TimeoutException); JsonException
            // covers a response that returned 2xx but did not parse. All three are the same
            // fact from BIT's point of view: the identity engine could not be reached right now.
            logger.LogWarning(
                ex, "PIMA evaluation request failed for correlation {CorrelationId}.", request.CorrelationId);

            return Result<IdentityEvaluationOutcome>.Fail(
                TourismErrorCodes.IdentityServiceUnavailable, "The identity engine could not be reached.");
        }
    }

    private static PimaEvaluationRequest ToWireRequest(IdentityEvaluationRequest request) => new(
        new PimaEvaluationContext(
            request.TenantId,
            request.CorrelationId,
            request.OrganizationId,
            request.BusinessLineId,
            request.RequestedByUserId),
        new PimaSubject(
            PlatformUserId: null,
            DisplayName: null,
            [.. request.Contacts.Select(c => new PimaContact((int)c.Channel, c.Value))]),
        request.AssertedPossession is { Count: > 0 } assertions
            ? [.. assertions.Select(a => new PimaAssertedPossession((int)a.Channel, a.Value, a.ConfirmedCount, a.LastConfirmedAtUtc))]
            : null);

    // ---- PIMA's wire contract, declared independently on BIT's side of the boundary ----

    private sealed record PimaEvaluationContext(
        Guid TenantId,
        string CorrelationId,
        Guid? OrganizationId,
        Guid? BusinessLineId,
        Guid? RequestedByUserId);

    private sealed record PimaContact(int Channel, string Value);

    private sealed record PimaAssertedPossession(int Channel, string Value, int ConfirmedCount, DateTime? LastConfirmedAtUtc);

    private sealed record PimaSubject(Guid? PlatformUserId, string? DisplayName, IReadOnlyList<PimaContact> Contacts);

    private sealed record PimaEvaluationRequest(
        PimaEvaluationContext Context, PimaSubject Subject, IReadOnlyList<PimaAssertedPossession>? AssertedPossession);

    private sealed record PimaScore(int? Value, decimal Coverage);

    /// <summary>
    /// PIMA's answer, in the fields BIT reads.
    ///
    /// <c>reportStanding</c> is an int on the wire, mapped onto BIT's own enum below rather
    /// than shared as a type. An unknown value is read as "nothing stands" — a number this
    /// version does not recognise cannot be turned into a claim against an operator, and
    /// guessing at the worst meaning of it would let a future PIMA release silently strip
    /// badges here.
    /// </summary>
    private sealed record PimaEvaluationResponse(
        Guid Id, PimaScore Score, int ReportStanding, string? PublicDirectoryId);
}
