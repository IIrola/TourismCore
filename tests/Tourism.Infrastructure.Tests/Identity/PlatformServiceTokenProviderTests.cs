using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;
using Tourism.Infrastructure.Identity;

namespace Tourism.Infrastructure.Tests.Identity;

/// <summary>
/// A ten-minute token fetched on every outbound call would put Platform on the critical path
/// of every PIMA evaluation — see the type doc on <see cref="PlatformServiceTokenProvider"/>.
/// These tests are what actually proves the cache and the exchange gate do their job, rather
/// than merely being present in the code.
/// </summary>
public sealed class PlatformServiceTokenProviderTests
{
    private const string Audience = "PimaCore.Services";
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = Now;
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        // A new HttpClient per call, same handler underneath — exactly how the real
        // IHttpClientFactory behaves, and why counting has to happen on the handler, not on
        // how many times CreateClient itself was called.
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://platform.test/") };
    }

    private static StubHttpMessageHandler GivenPlatformIssues(string token, DateTime expiresAtUtc) => new(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = token, expiresAtUtc })
        });

    private static PlatformServiceTokenProvider CreateProvider(StubHttpMessageHandler handler, TestClock clock)
    {
        var options = Options.Create(new PlatformOptions
        {
            BaseUrl = "https://platform.test/",
            ClientId = "TourismCore",
            ClientSecret = "s3cr3t"
        });

        return new PlatformServiceTokenProvider(
            new FakeHttpClientFactory(handler), options, clock, NullLogger<PlatformServiceTokenProvider>.Instance);
    }

    [Fact]
    public async Task Two_sequential_calls_for_the_same_audience_result_in_one_exchange()
    {
        var clock = new TestClock();
        var handler = GivenPlatformIssues("token-1", Now.AddMinutes(10));
        var provider = CreateProvider(handler, clock);

        var first = await provider.GetTokenAsync(Audience);
        var second = await provider.GetTokenAsync(Audience);

        first.Should().Be("token-1");
        second.Should().Be("token-1");
        handler.CallCount.Should().Be(1, "the second call should be served from cache, not from a second exchange");
    }

    [Fact]
    public async Task Concurrent_calls_for_the_same_audience_result_in_one_exchange()
    {
        var clock = new TestClock();
        var handler = GivenPlatformIssues("token-1", Now.AddMinutes(10));
        var provider = CreateProvider(handler, clock);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => provider.GetTokenAsync(Audience)));

        results.Should().OnlyContain(t => t == "token-1");
        handler.CallCount.Should().Be(1, "the exchange gate must serialize concurrent callers onto a single exchange");
    }

    [Fact]
    public async Task A_token_within_the_renewal_margin_of_expiring_is_exchanged_again()
    {
        var clock = new TestClock();
        var handler = GivenPlatformIssues("token-1", Now.AddMinutes(10));
        var provider = CreateProvider(handler, clock);

        await provider.GetTokenAsync(Audience);

        // Nine minutes and five seconds in: inside the 60-second renewal margin of a token
        // that was issued for ten minutes. A request presenting this token could still be in
        // flight when it actually expired, so it must be treated as stale already.
        clock.UtcNow = Now.AddMinutes(9).AddSeconds(5);
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = "token-2", expiresAtUtc = clock.UtcNow.AddMinutes(10) })
        };

        var renewed = await provider.GetTokenAsync(Audience);

        renewed.Should().Be("token-2");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task A_token_well_before_its_renewal_margin_is_not_exchanged_again()
    {
        var clock = new TestClock();
        var handler = GivenPlatformIssues("token-1", Now.AddMinutes(10));
        var provider = CreateProvider(handler, clock);

        await provider.GetTokenAsync(Audience);

        clock.UtcNow = Now.AddMinutes(5);
        var stillCached = await provider.GetTokenAsync(Audience);

        stillCached.Should().Be("token-1");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Different_audiences_are_cached_and_exchanged_independently()
    {
        var clock = new TestClock();
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { accessToken = $"token-{callCount}", expiresAtUtc = Now.AddMinutes(10) })
            };
        });
        var provider = CreateProvider(handler, clock);

        var forPima = await provider.GetTokenAsync("PimaCore.Services");
        var forSomethingElse = await provider.GetTokenAsync("OtherService.Services");
        var forPimaAgain = await provider.GetTokenAsync("PimaCore.Services");

        forPima.Should().Be("token-1");
        forSomethingElse.Should().Be("token-2");
        forPimaAgain.Should().Be("token-1", "the second audience's exchange must not have evicted the first's cache entry");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task The_client_secret_is_never_written_to_the_request_body_in_the_clear_when_logging_is_the_concern()
    {
        // Not a claim that the secret is absent from the wire (Platform must receive it to
        // authenticate the exchange) — only that this class never routes it anywhere else,
        // such as a log message. See PlatformServiceTokenProvider's LogDebug call, which
        // names only the audience and the expiry.
        var clock = new TestClock();
        var handler = GivenPlatformIssues("token-1", Now.AddMinutes(10));
        var provider = CreateProvider(handler, clock);

        await provider.GetTokenAsync(Audience);

        handler.RequestBodies.Should().ContainSingle(b => b != null && b.Contains("s3cr3t"),
            "Platform still needs the secret on the wire to authenticate the exchange itself");
    }
}
