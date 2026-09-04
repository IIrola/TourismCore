using Tourism.Domain.Common;

namespace Tourism.Application.PublicDirectory.Ports;

/// <summary>A contact as the directory takes it, in BIT's own vocabulary.</summary>
public enum DirectoryChannel
{
    Email = 0,
    Phone = 1
}

/// <param name="Value">Masked unless the operator consented to the full value being shown.</param>
public sealed record PublishedContact(DirectoryChannel Channel, string Value, bool IsMasked);

/// <summary>
/// What the identity engine agrees may be published about a subject.
///
/// BIT's own type, like <see cref="Domain.Badges.IdentityAssessment"/>: the two services share
/// the shape of an answer, never an assembly. Every field is nullable because every one of
/// them is a separate consent, and "not consented" and "no value" arrive the same way — which
/// is deliberate on PIMA's side, so that a page cannot leak the consent decision itself.
/// </summary>
public sealed record PublishedIdentity(
    string PublicDirectoryId,
    string? DisplayName,
    int? Score,
    string? RiskLevel,
    decimal? Coverage,
    string? ReportStanding,
    string? Description,
    IReadOnlyList<PublishedContact> Contacts,
    DateTime? LastEvaluatedAtUtc);

/// <summary>
/// BIT's view of the identity engine's public directory.
///
/// The split this port exists to hold: <b>PIMA publishes facts, BIT publishes a page.</b> The
/// legacy served both from the engine, anonymously, assembling identity, risk facts and
/// tourism business data into one response — so the engine had to know what a tourism listing
/// looked like, and every vertical would have had to add its fields to the engine's own DTO.
///
/// Failures come back as <see cref="Result{T}"/>. The engine being unreachable is ordinary, and
/// what a public page does about it is a business decision.
/// </summary>
public interface IPublicDirectoryClient
{
    /// <summary>
    /// The published facts behind an identifier, or a not-found result.
    ///
    /// A withdrawn subject and one who never existed come back identically — the engine
    /// refuses to distinguish them, so an old link cannot confirm somebody is on the platform
    /// after they asked not to be.
    /// </summary>
    Task<Result<PublishedIdentity>> GetPublishedAsync(
        string publicDirectoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The identifier for a contact, if its owner agreed to being findable.
    ///
    /// Read-only, and worth saying because its predecessor was not: the legacy's public
    /// lookup called a paid provider, created an identity profile and emailed an activation
    /// link to whatever address had been typed into a search box.
    /// </summary>
    Task<Result<string?>> LookupAsync(
        DirectoryChannel channel, string value, CancellationToken cancellationToken = default);
}
