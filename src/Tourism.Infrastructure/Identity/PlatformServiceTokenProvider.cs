using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;

namespace Tourism.Infrastructure.Identity;

/// <summary>
/// Exchanges BIT's own service-client credentials for a short-lived token, by calling
/// Platform's "POST /api/v1/auth/service-token" — the machine-to-machine counterpart of a
/// user login.
///
/// Registered as a singleton (see <c>DependencyInjection.AddInfrastructure</c>) so the cache
/// below survives across requests; the <see cref="HttpClient"/> itself is obtained per call
/// from <see cref="IHttpClientFactory"/> instead of being injected directly, which is what
/// lets this class be a singleton while still getting pooled, DNS-refreshing connections
/// rather than one held open for the process lifetime.
/// </summary>
public sealed class PlatformServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<PlatformOptions> options,
    IClock clock,
    ILogger<PlatformServiceTokenProvider> logger) : IServiceTokenProvider
{
    /// <summary>Name of the named <see cref="HttpClient"/> registered for calls to Platform.</summary>
    public const string HttpClientName = "Platform";

    /// <summary>
    /// Platform's controllers serialize with ASP.NET Core's default camelCase MVC JSON
    /// options; <see cref="System.Net.Http.Json"/>'s own defaults are plain
    /// <see cref="JsonSerializerOptions.Default"/> (PascalCase, case-sensitive), which would
    /// silently fail to bind "accessToken"/"expiresAtUtc" onto this client's PascalCase
    /// record properties. Matching the wire format explicitly avoids that mismatch.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Renew this long before the token's real (ten-minute, per Platform's ServiceTokenOptions)
    /// expiry, so a request already in flight when the cached entry goes stale never ends up
    /// presenting a token that expires mid-call.
    /// </summary>
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(60);

    private readonly PlatformOptions _options = options.Value;

    // Keyed by audience: BIT may eventually call more than one service, and each audience's
    // token is cached and renewed independently.
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _exchangeGates = new();

    public async Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        if (TryGetFresh(audience, out var cachedToken))
            return cachedToken;

        // One exchange per audience at a time. Without this gate, every request that finds
        // the cache stale at the same moment fires its own credential exchange against
        // Platform — exactly the "on the critical path of every evaluation" cost caching
        // exists to avoid, just moved from "every call" to "every call after expiry".
        var gate = _exchangeGates.GetOrAdd(audience, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check: whoever held the gate before us may have just refreshed it.
            if (TryGetFresh(audience, out cachedToken))
                return cachedToken;

            var exchanged = await ExchangeAsync(audience, cancellationToken);
            _cache[audience] = exchanged;

            // The expiry is safe to log; the token and the client secret never are.
            logger.LogDebug(
                "Exchanged a Platform service token for audience {Audience}, valid until {ExpiresAtUtc:o}.",
                audience, exchanged.ExpiresAtUtc);

            return exchanged.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFresh(string audience, out string accessToken)
    {
        if (_cache.TryGetValue(audience, out var cached) && cached.ExpiresAtUtc - RenewalMargin > clock.UtcNow)
        {
            accessToken = cached.AccessToken;
            return true;
        }

        accessToken = string.Empty;
        return false;
    }

    private async Task<CachedToken> ExchangeAsync(string audience, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var requestBody = new ServiceTokenRequest(_options.ClientId, _options.ClientSecret, audience);

        using var response = await client.PostAsJsonAsync(
            "api/v1/auth/service-token", requestBody, JsonOptions, cancellationToken);

        // The body of a failed exchange is never inspected or logged: it could echo back
        // request details, and this call is not allowed to leak anything about the secret it
        // just sent.
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Platform returned an empty service-token response.");

        return new CachedToken(payload.AccessToken, payload.ExpiresAtUtc);
    }

    private sealed record CachedToken(string AccessToken, DateTime ExpiresAtUtc);

    private sealed record ServiceTokenRequest(string ClientId, string ClientSecret, string Audience);

    private sealed record ServiceTokenResponse(string AccessToken, DateTime ExpiresAtUtc);
}
