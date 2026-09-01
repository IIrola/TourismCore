namespace Tourism.Infrastructure.Security;

/// <summary>
/// Binds the "Jwt" configuration section used to validate the end-user access tokens
/// Platform issues. BIT never issues tokens of its own here — it only has to trust the same
/// issuer, audience and signing key Platform's own resource servers already trust.
///
/// Validated on startup by <c>AddInfrastructure</c> (<c>ValidateOnStart</c>) so a missing or
/// weak signing key fails the app immediately instead of surfacing as a cryptic error on the
/// first authenticated request.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Shortest signing key HMAC-SHA256 should be used with (256 bits as UTF-8 bytes).</summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>Must match Platform's own "Jwt:Issuer".</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Must match Platform's own "Jwt:Audience" for user-facing access tokens.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Never set in appsettings.json. Provided via the Jwt__SigningKey environment variable
    /// or user-secrets so it never lands in source control. Must be identical to Platform's
    /// own signing key — this is what lets BIT trust a token it never issued.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}
