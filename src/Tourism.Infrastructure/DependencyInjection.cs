using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tourism.Application.Common.Ports;
using Tourism.Application.Identity.Ports;
using Tourism.Application.Organizations.Ports;
using Tourism.Application.PublicDirectory.Ports;
using Tourism.Infrastructure.Common;
using Tourism.Infrastructure.Identity;
using Tourism.Infrastructure.PublicDirectory;
using Tourism.Infrastructure.Persistence;
using Tourism.Infrastructure.Persistence.Repositories;
using Tourism.Infrastructure.Security;

namespace Tourism.Infrastructure;

/// <summary>Composition root for the BIT infrastructure layer.</summary>
public static class DependencyInjection
{
    private const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Pinned instead of <c>ServerVersion.AutoDetect</c>: auto-detection opens a connection
    /// while the service collection is being built, which would make startup and
    /// design-time tooling (migrations) depend on a reachable database. Same version
    /// Platform and PIMA pin to.
    /// </summary>
    private static readonly MariaDbServerVersion ServerVersion = new(new Version(11, 4, 0));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<TourismDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITourismOrganizationProfileRepository, TourismOrganizationProfileRepository>();

        // Reads the current HttpContext, so it cannot outlive the request the way a
        // singleton would need to.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Fails startup immediately (ValidateOnStart) rather than on the first authenticated
        // request when the signing key used to validate Platform's user tokens is missing or
        // too weak for HMAC-SHA256.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= JwtOptions.MinimumSigningKeyLength,
                $"Jwt:SigningKey is required and must be at least {JwtOptions.MinimumSigningKeyLength} characters. " +
                "Provide it via an environment variable or user-secrets — never in appsettings.json.")
            .ValidateOnStart();

        AddPlatformClient(services, configuration);
        AddPimaClient(services, configuration);

        return services;
    }

    /// <summary>
    /// Wires the credential exchange BIT performs against Platform. A NAMED
    /// <see cref="HttpClient"/> rather than the typed-client sugar
    /// (<c>AddHttpClient&lt;TClient,TImplementation&gt;</c>) on purpose:
    /// <see cref="PlatformServiceTokenProvider"/> must be a singleton for its token cache to
    /// survive across requests, and the typed-client registration would instead give it a
    /// transient lifetime tied to the HttpClient's own — a fresh, empty cache on every
    /// resolution, defeating the whole point of caching.
    /// </summary>
    private static void AddPlatformClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PlatformOptions>()
            .Bind(configuration.GetSection(PlatformOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Platform:BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Platform:ClientId is required.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ClientSecret),
                "Platform:ClientSecret is required. Provide it via the Platform__ClientSecret " +
                "environment variable or user-secrets — never in appsettings.json.")
            .ValidateOnStart();

        services.AddHttpClient(PlatformServiceTokenProvider.HttpClientName, (sp, client) =>
        {
            var platformOptions = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
            client.BaseAddress = new Uri(platformOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IServiceTokenProvider, PlatformServiceTokenProvider>();

        // The other direction: BIT asking Platform a question rather than being authenticated
        // by it. Its own typed client so a slow possession lookup cannot hold up token
        // issuance, which every other call depends on.
        services.AddHttpClient<IPossessionClient, HttpPossessionClient>((sp, client) =>
        {
            var platformOptions = sp.GetRequiredService<IOptions<PlatformOptions>>().Value;
            client.BaseAddress = new Uri(platformOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    /// <summary>
    /// Wires the call BIT makes to PIMA to request an evaluation. Uses the typed-client
    /// sugar: <see cref="HttpIdentityEvaluationClient"/> holds no state across calls (all
    /// caching lives in <see cref="IServiceTokenProvider"/>), so the shorter-lived client
    /// this registration produces is exactly what is wanted.
    /// </summary>
    private static void AddPimaClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PimaOptions>()
            .Bind(configuration.GetSection(PimaOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Pima:BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Pima:Audience is required.")
            .Validate(o => o.TimeoutSeconds > 0, "Pima:TimeoutSeconds must be positive.")
            .ValidateOnStart();

        services.AddHttpClient<IIdentityEvaluationClient, HttpIdentityEvaluationClient>((sp, client) =>
        {
            var pimaOptions = sp.GetRequiredService<IOptions<PimaOptions>>().Value;
            client.BaseAddress = new Uri(pimaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(pimaOptions.TimeoutSeconds);
        });

        // Same service, same credentials, same options — a second typed client rather than a
        // second configuration section. Giving the directory its own would mean two token
        // caches for one audience.
        services.AddHttpClient<IPublicDirectoryClient, HttpPublicDirectoryClient>((sp, client) =>
        {
            var pimaOptions = sp.GetRequiredService<IOptions<PimaOptions>>().Value;
            client.BaseAddress = new Uri(pimaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(pimaOptions.TimeoutSeconds);
        });
    }
}
