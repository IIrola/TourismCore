using Microsoft.Extensions.DependencyInjection;

namespace Tourism.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AssemblyReference>());

        return services;
    }
}

/// <summary>Anchor for assembly scanning, so registration does not depend on a type name.</summary>
public sealed class AssemblyReference;
