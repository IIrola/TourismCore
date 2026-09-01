namespace Tourism.Infrastructure.Identity;

/// <summary>
/// Binds the "Pima" configuration section: where the identity engine lives and which
/// audience BIT must request a service token for before calling it. Validated on startup by
/// <c>AddInfrastructure</c> (<c>ValidateOnStart</c>).
/// </summary>
public sealed class PimaOptions
{
    public const string SectionName = "Pima";

    /// <summary>Base address of PIMA's API, e.g. "https://pima.internal".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The audience PIMA validates incoming service tokens against (its own "Jwt:Audience").
    /// Passed to <see cref="Tourism.Application.Common.Ports.IServiceTokenProvider"/> so the
    /// token BIT presents is actually scoped to PIMA, not to some other service.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// How long BIT waits for PIMA to answer before treating it as unreachable. Short on
    /// purpose: a tourism listing waiting on a badge decision should fail fast into
    /// <see cref="Tourism.Domain.Common.TourismErrorCodes.IdentityServiceUnavailable"/>
    /// rather than hang the caller's request.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
