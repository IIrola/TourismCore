using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Domain.Common;
using Tourism.Infrastructure.Identity;

namespace Tourism.Infrastructure.Tests.Identity;

/// <summary>
/// BIT's HTTP boundary with PIMA. These tests exist for the two facts the task explicitly
/// depends on: a null <c>score.value</c> is PIMA saying "inconclusive", not an error, and any
/// failure on the wire must come back as <see cref="Result{T}"/> rather than an exception —
/// the handler on the other end decides what a tourism listing does about an unreachable
/// identity engine, and it cannot do that from inside a catch block.
/// </summary>
public sealed class HttpIdentityEvaluationClientTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IServiceTokenProvider _tokenProvider = Substitute.For<IServiceTokenProvider>();

    public HttpIdentityEvaluationClientTests()
        => _tokenProvider.GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("service-token-1");

    private HttpIdentityEvaluationClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pima.test/") };
        var options = Options.Create(new PimaOptions
        {
            BaseUrl = "https://pima.test/",
            Audience = "PimaCore.Services",
            TimeoutSeconds = 5
        });
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        return new HttpIdentityEvaluationClient(
            httpClient, _tokenProvider, options, clock, NullLogger<HttpIdentityEvaluationClient>.Instance);
    }

    private static IdentityEvaluationRequest AnyRequest() => new(
        Guid.NewGuid(), Guid.NewGuid(), "trace-1", [new EvaluationContact(EvaluationChannel.Email, "op@example.com")]);

    [Fact]
    public async Task A_null_score_value_maps_to_an_inconclusive_assessment_not_an_error()
    {
        var evaluationId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = evaluationId, score = new { value = (int?)null, coverage = 0m } })
        });

        var result = await CreateClient(handler).EvaluateAsync(AnyRequest());

        result.IsSuccess.Should().BeTrue("an inconclusive evaluation is a valid outcome, not a failure");
        result.Value!.EvaluationId.Should().Be(evaluationId);
        result.Value.Assessment.Score.Should().BeNull();
        result.Value.Assessment.IsConclusive.Should().BeFalse();
    }

    [Fact]
    public async Task A_conclusive_score_maps_its_value_and_coverage_through()
    {
        var evaluationId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = evaluationId, score = new { value = 820, coverage = 0.75m } })
        });

        var result = await CreateClient(handler).EvaluateAsync(AnyRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Assessment.Score.Should().Be(820);
        result.Value.Assessment.Coverage.Should().Be(0.75m);
        result.Value.Assessment.EvaluatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task A_500_from_PIMA_maps_to_IdentityServiceUnavailable_not_an_exception()
    {
        var handler = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await CreateClient(handler).EvaluateAsync(AnyRequest());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(TourismErrorCodes.IdentityServiceUnavailable);
    }

    [Fact]
    public async Task A_connection_failure_maps_to_IdentityServiceUnavailable_not_an_exception()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new HttpRequestException("Connection refused."));

        var act = () => CreateClient(handler).EvaluateAsync(AnyRequest());

        var result = await act.Should().NotThrowAsync();
        result.Subject.IsSuccess.Should().BeFalse();
        result.Subject.ErrorCode.Should().Be(TourismErrorCodes.IdentityServiceUnavailable);
    }

    [Fact]
    public async Task A_malformed_response_body_maps_to_IdentityServiceUnavailable_not_an_exception()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json", System.Text.Encoding.UTF8, "application/json")
        });

        var result = await CreateClient(handler).EvaluateAsync(AnyRequest());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(TourismErrorCodes.IdentityServiceUnavailable);
    }

    [Fact]
    public async Task The_service_token_is_sent_as_a_bearer_header()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("service-token-1");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid(), score = new { value = 500, coverage = 1m } })
            };
        });

        var result = await CreateClient(handler).EvaluateAsync(AnyRequest());

        result.IsSuccess.Should().BeTrue();
        await _tokenProvider.Received(1).GetTokenAsync("PimaCore.Services", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_wire_request_carries_context_and_contacts_in_PIMAs_shape()
    {
        var request = new IdentityEvaluationRequest(
            TenantId: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            CorrelationId: "trace-xyz",
            Contacts: [new EvaluationContact(EvaluationChannel.Phone, "+525555550123")]);

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { id = Guid.NewGuid(), score = new { value = 500, coverage = 1m } })
        });

        await CreateClient(handler).EvaluateAsync(request);

        var body = handler.RequestBodies.Should().ContainSingle().Subject!;
        body.Should().Contain("\"tenantId\"");
        body.Should().Contain("trace-xyz");
        // The default JSON encoder escapes '+' as +, so assert through a parsed
        // document rather than raw string containment for the phone value.
        using var parsed = System.Text.Json.JsonDocument.Parse(body);
        parsed.RootElement.GetProperty("subject").GetProperty("contacts")[0].GetProperty("value")
            .GetString().Should().Be("+525555550123");
        // EvaluationChannel.Phone = 1, serialized as a bare number, matching PIMA's own
        // ContactChannel enum (Email = 0, Phone = 1) — never as the string "Phone".
        body.Should().Contain("\"channel\":1");
    }
}
