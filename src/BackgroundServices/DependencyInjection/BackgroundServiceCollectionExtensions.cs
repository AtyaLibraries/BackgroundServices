using Atya.Foundation.Guards;
using Atya.Hosting.BackgroundServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    /// <param name="configure">
    /// An optional callback that configures the named options of this worker
    /// (see <see cref="PeriodicBackgroundServiceOptionNames"/>). The options are validated when the host starts.
    /// </param>
    /// <returns>The same service collection instance.</returns>
    public static IServiceCollection AddAtyaBackgroundService<TService>(
        this IServiceCollection services,
        Action<PeriodicBackgroundServiceOptions>? configure = null)
        where TService : PeriodicBackgroundService
    {
        services = Guard.AgainstNull(services);
        AddWorkerOptions<TService>(services, configure);
        services.AddHostedService<TService>();
        return services;
    }

    /// <summary>
    /// Registers a hosted service that creates a fresh dependency injection scope for each iteration.
    /// </summary>
    /// <typeparam name="TService">The scoped periodic hosted service implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional callback that configures the named options of this worker
    /// (see <see cref="PeriodicBackgroundServiceOptionNames"/>). The options are validated when the host starts.
    /// </param>
    /// <returns>The same service collection instance.</returns>
    public static IServiceCollection AddAtyaScopedBackgroundService<TService>(
        this IServiceCollection services,
        Action<PeriodicBackgroundServiceOptions>? configure = null)
        where TService : ScopedPeriodicBackgroundService
    {
        services = Guard.AgainstNull(services);
        AddWorkerOptions<TService>(services, configure);
        services.AddHostedService<TService>();
        return services;
    }

    private static void AddWorkerOptions<TService>(IServiceCollection services, Action<PeriodicBackgroundServiceOptions>? configure)
    {
        string name = PeriodicBackgroundServiceOptionNames.For<TService>();
        OptionsBuilder<PeriodicBackgroundServiceOptions> builder = services.AddOptions<PeriodicBackgroundServiceOptions>(name);
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        services.AddSingleton<IValidateOptions<PeriodicBackgroundServiceOptions>>(new PeriodicBackgroundServiceOptionsValidator(name));
        builder.ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
    }
}
