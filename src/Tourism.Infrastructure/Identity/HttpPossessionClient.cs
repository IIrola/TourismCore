using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Domain.Common;

namespace Tourism.Infrastructure.Identity;

/// <summary>
/// BIT's HTTP boundary with Platform for possession facts.
///
/// The second direction of service-to-service traffic in this system: until now BIT only ever
/// asked Platform for a token. Asking it a question needs an audience of its own, granted to
/// this client separately from PIMA's — being allowed to evaluate identities never implied
/// being allowed to read what Platform knows about a contact.
///
/// Failures come back as a <see cref="Result"/>, never as an exception, for the same reason
/// the identity client does it: the handler decides what a tourism listing does when a
/// dependency is down, and it cannot decide that from inside a catch block.
/// </summary>
public sealed class HttpPossessionClient(
    HttpClient httpClient,
    IServiceTokenProvider tokenProvider,
    IOptions<PlatformOptions> options,
    ILogger<HttpPossessionClient> logger) : IPossessionClient
{
    private const string PossessionPath = "api/v1/possession/query";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PlatformOptions _options = options.Value;

    /// <summary>Platform's wire vocabulary. Declared here rather than shared, on purpose.</summary>
    private sealed record ContactRef(int Channel, string Value);

    private sealed record QueryBody(IReadOnlyList<ContactRef> Contacts);

    private sealed record FactBody(int Channel, string Value, DateTime ConfirmedAtUtc);

    public async Task<Result<IReadOnlyList<ConfirmedContact>>> GetConfirmedAsync(
        IReadOnlyList<EvaluationContact> contacts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contacts);

        if (contacts.Count == 0)
            return Result<IReadOnlyList<ConfirmedContact>>.Ok([]);

        try
        {
            var token = await tokenProvider.GetTokenAsync(_options.ServiceAudience, cancellationToken);

            var body = new QueryBody([.. contacts.Select(c => new ContactRef((int)c.Channel, c.Value))]);

            using var request = new HttpRequestMessage(HttpMethod.Post, PossessionPath)
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Platform answered {StatusCode} when asked about possession.", (int)response.StatusCode);
                return Unavailable();
            }

            var facts = await response.Content.ReadFromJsonAsync<List<FactBody>>(JsonOptions, cancellationToken);
            if (facts is null)
                return Unavailable();

            return Result<IReadOnlyList<ConfirmedContact>>.Ok(
            [
                .. facts.Select(f => new ConfirmedContact(
                    (EvaluationChannel)f.Channel, f.Value, f.ConfirmedAtUtc))
            ]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not reach Platform to ask about possession.");
            return Unavailable();
        }
    }

    private static Result<IReadOnlyList<ConfirmedContact>> Unavailable()
        => Result<IReadOnlyList<ConfirmedContact>>.Fail(
            TourismErrorCodes.PlatformUnavailable, "Platform could not be reached.");
}
