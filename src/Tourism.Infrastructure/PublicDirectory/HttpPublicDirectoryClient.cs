using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;
using Tourism.Application.PublicDirectory.Ports;
using Tourism.Domain.Common;
using Tourism.Infrastructure.Identity;

namespace Tourism.Infrastructure.PublicDirectory;

/// <summary>
/// Asks PIMA what may be published about a subject.
///
/// Shares the named client and the token provider with the evaluation client: it is the same
/// service, reached with the same credentials, and giving the directory its own would mean two
/// token caches for one audience.
/// </summary>
public sealed class HttpPublicDirectoryClient(
    HttpClient httpClient,
    IServiceTokenProvider tokens,
    IOptions<PimaOptions> options,
    ILogger<HttpPublicDirectoryClient> logger) : IPublicDirectoryClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PimaOptions _options = options.Value;

    public async Task<Result<PublishedIdentity>> GetPublishedAsync(
        string publicDirectoryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicDirectoryId))
            return Result<PublishedIdentity>.Fail(TourismErrorCodes.NotFound);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"api/v1/public-directory/{Uri.EscapeDataString(publicDirectoryId)}");

            await AuthorizeAsync(request, cancellationToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The engine's answer, forwarded. It does not distinguish a subject who
                // withdrew from one who never existed, and neither may this.
                return Result<PublishedIdentity>.Fail(TourismErrorCodes.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The identity engine answered {StatusCode} for a published profile.",
                    (int)response.StatusCode);

                return Result<PublishedIdentity>.Fail(TourismErrorCodes.IdentityServiceUnavailable);
            }

            var wire = await response.Content.ReadFromJsonAsync<PublishedWire>(Json, cancellationToken);
            if (wire is null)
                return Result<PublishedIdentity>.Fail(TourismErrorCodes.IdentityServiceUnavailable);

            return Result<PublishedIdentity>.Ok(Map(wire));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not reach the identity engine for a published profile.");
            return Result<PublishedIdentity>.Fail(TourismErrorCodes.IdentityServiceUnavailable);
        }
    }

    public async Task<Result<string?>> LookupAsync(
        DirectoryChannel channel, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/public-directory/lookup");
            await AuthorizeAsync(request, cancellationToken);

            // A POST, and the contact in the body: a contact is personal data and has no
            // business in a URL, a proxy log or a browser history. The legacy took it as a
            // query parameter on an anonymous route.
            //
            // Serialized up front so the body carries a Content-Length rather than going out
            // chunked — a failure mode this migration has already paid for once.
            var body = JsonSerializer.Serialize(
                new LookupWire(new ContactWire((int)channel, value)), Json);

            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The identity engine answered {StatusCode} for a directory lookup.",
                    (int)response.StatusCode);

                return Result<string?>.Fail(TourismErrorCodes.IdentityServiceUnavailable);
            }

            var match = await response.Content.ReadFromJsonAsync<MatchWire>(Json, cancellationToken);

            // A null identifier is a successful "no". The engine deliberately gives one shape
            // of answer for every negative, and turning that into a failure here would let a
            // caller tell them apart by watching which error came back.
            return Result<string?>.Ok(match?.PublicDirectoryId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not reach the identity engine for a directory lookup.");
            return Result<string?>.Fail(TourismErrorCodes.IdentityServiceUnavailable);
        }
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await tokens.GetTokenAsync(_options.Audience, cancellationToken));

    private static PublishedIdentity Map(PublishedWire wire) => new(
        wire.PublicDirectoryId ?? string.Empty,
        wire.DisplayName,
        wire.Score,
        wire.RiskLevel,
        wire.Coverage,
        wire.ReportStanding,
        wire.Description,
        [.. (wire.Contacts ?? []).Select(c => new PublishedContact(
            Enum.IsDefined(typeof(DirectoryChannel), c.Channel)
                ? (DirectoryChannel)c.Channel
                : DirectoryChannel.Email,
            c.Value ?? string.Empty,
            c.IsMasked))],
        wire.LastEvaluatedAtUtc);

    private sealed record ContactWire(int Channel, string Value);

    private sealed record LookupWire(ContactWire Contact);

    private sealed record MatchWire(string? PublicDirectoryId);

    private sealed record PublishedContactWire(int Channel, string? Value, bool IsMasked);

    private sealed record PublishedWire(
        string? PublicDirectoryId,
        string? DisplayName,
        int? Score,
        string? RiskLevel,
        decimal? Coverage,
        string? ReportStanding,
        string? Description,
        IReadOnlyList<PublishedContactWire>? Contacts,
        DateTime? LastEvaluatedAtUtc);
}
