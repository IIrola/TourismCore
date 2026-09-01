namespace Tourism.Infrastructure.Identity;

/// <summary>
/// Binds the "Platform" configuration section: how BIT reaches Platform as a service client
/// of its own, to exchange credentials for the tokens it presents when calling PIMA (or any
/// other audience Platform has granted it). Validated on startup by
/// <c>AddInfrastructure</c> (<c>ValidateOnStart</c>), the same way Platform and PIMA validate
/// their own Jwt options.
/// </summary>
public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Base address of Platform's API, e.g. "https://platform.internal".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>BIT's own service-client id, registered in Platform ahead of time.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Never set in appsettings.json. Provided via the Platform__ClientSecret environment
    /// variable or user-secrets so it never lands in source control.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}
