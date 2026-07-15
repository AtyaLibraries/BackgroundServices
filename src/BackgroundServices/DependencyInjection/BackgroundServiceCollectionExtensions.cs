using Atya.Foundation.Guards;
using Atya.Hosting.BackgroundServices;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides dependency injection helpers for Atya background services.
/// </summary>
public static class BackgroundServiceCollectionExtensions
{
    /// <summary>
    /// Registers a hosted service implemented with Atya background-service helpers.
    /// </summary>
    /// <typeparam name="TService">The hosted service implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional callback that configures periodic execution options.</param>
    /// <returns>The same service collection instance.</returns>
    public static IServiceCollection AddAtyaBackgroundService<TService>(
        this IServiceCollection services,
        Action<PeriodicBackgroundServiceOptions>? configure = null)
        where TService : PeriodicBackgroundService
    {
        services = Guard.AgainstNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<TService>();
        return services;
    }

    /// <summary>
    /// Registers a hosted service that creates a fresh dependency injection scope for each iteration.
    /// </summary>
    /// <typeparam name="TService">The scoped periodic hosted service implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional callback that configures periodic execution options.</param>
    /// <returns>The same service collection instance.</returns>
    public static IServiceCollection AddAtyaScopedBackgroundService<TService>(
        this IServiceCollection services,
        Action<PeriodicBackgroundServiceOptions>? configure = null)
        where TService : ScopedPeriodicBackgroundService
    {
        services = Guard.AgainstNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<TService>();
        return services;
    }
}
