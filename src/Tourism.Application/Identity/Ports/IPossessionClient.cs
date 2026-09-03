using Tourism.Domain.Common;

namespace Tourism.Application.Identity.Ports;

/// <summary>
/// A contact Platform has seen somebody prove control of, and when.
///
/// BIT's own words for it, like every shape on a service boundary here: there is no project
/// reference to Platform, so the wire contract is declared independently on both sides.
/// </summary>
public sealed record ConfirmedContact(EvaluationChannel Channel, string Value, DateTime ConfirmedAtUtc);

/// <summary>
/// Asks Platform which contacts it will vouch for.
///
/// This exists so that possession reaches the identity engine as a fact somebody established
/// rather than as a claim the caller typed. Possession carries more weight in an identity
/// score than any other single input, and before this port BIT forwarded whatever arrived in
/// its own request body — which meant anybody calling BIT could award themselves that weight.
///
/// Failure is not an exception: an unreachable Platform means the evaluation proceeds with
/// less evidence and honest coverage, and that is the caller's judgement to make, not this
/// client's.
/// </summary>
public interface IPossessionClient
{
    Task<Result<IReadOnlyList<ConfirmedContact>>> GetConfirmedAsync(
        IReadOnlyList<EvaluationContact> contacts, CancellationToken cancellationToken = default);
}
