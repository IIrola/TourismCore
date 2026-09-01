namespace Tourism.Infrastructure.Tests;

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> double: answers every request with whatever
/// <see cref="Respond"/> currently returns, and counts how many requests actually reached it
/// — which is what the token-caching tests need to assert on (a cache hit must never reach
/// this handler at all).
///
/// The request body is captured as a string immediately, rather than the
/// <see cref="HttpRequestMessage"/> itself: the caller's pipeline disposes that message once
/// the send completes, so holding onto it for later assertions would risk reading a disposed
/// object.
/// </summary>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;

    public int CallCount { get; private set; }

    public List<string?> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return Respond(request);
    }
}
