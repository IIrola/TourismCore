using Tourism.Domain.Badges;
using Tourism.Domain.Common;

namespace Tourism.Application.Identity.Ports;

public enum EvaluationChannel
{
    Email = 0,
    Phone = 1
}

public sealed record EvaluationContact(EvaluationChannel Channel, string Value);

/// <summary>A possession fact BIT vouches for, because Platform proved it and told BIT.</summary>
public sealed record AssertedPossession(
    EvaluationChannel Channel, string Value, int ConfirmedCount, DateTime? LastConfirmedAtUtc);

public sealed record IdentityEvaluationRequest(
    Guid TenantId,
    Guid OrganizationId,
    string CorrelationId,
    IReadOnlyList<EvaluationContact> Contacts,
    IReadOnlyList<AssertedPossession>? AssertedPossession = null,
    Guid? BusinessLineId = null,
    Guid? RequestedByUserId = null);

/// <param name="ReportStanding">What the incident reports against these contacts amount to,
/// as PIMA saw them when the evaluation closed. It arrives beside the score and never inside
/// it: what an unresolved or upheld accusation should cost a tourism listing is BIT's
/// judgement, and it can only make that judgement if the two are told apart.</param>
public sealed record IdentityEvaluationOutcome(
    Guid EvaluationId, IdentityAssessment Assessment, ReportStanding ReportStanding);

/// <summary>
/// BIT's view of the identity engine.
///
/// An HTTP boundary described in BIT's own vocabulary. There is no project reference to PIMA
/// and there must not be: the two services deploy independently, and a compile-time
/// dependency would quietly turn that into a lie. What BIT depends on is the shape of an
/// answer, and that shape lives here.
///
/// Failures come back as <see cref="Result{T}"/> rather than exceptions. An identity engine
/// being unreachable is an ordinary thing that will happen, and the caller has to decide what
/// a tourism listing does about it — which is a business decision, not a stack trace.
/// </summary>
public interface IIdentityEvaluationClient
{
    Task<Result<IdentityEvaluationOutcome>> EvaluateAsync(
        IdentityEvaluationRequest request, CancellationToken cancellationToken = default);
}
