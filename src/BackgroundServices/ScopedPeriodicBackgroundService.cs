using Atya.Foundation.Guards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Provides a <see cref="BackgroundService"/> base class that creates a fresh dependency injection scope for each iteration.
/// The scope is disposed asynchronously before the delay that follows the iteration starts.
/// </summary>
public abstract class ScopedPeriodicBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LoopSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedPeriodicBackgroundService"/> class.
    /// </summary>
    /// <param name="logger">The logger used to write lifecycle and failure events.</param>
    /// <param name="scopeFactory">The factory used to create one dependency injection scope per iteration.</param>
    /// <param name="options">
    /// The options monitor. The named instance for this worker's type (see
    /// <see cref="PeriodicBackgroundServiceOptionNames"/>) is read once and copied.
    /// </param>
    /// <param name="timeProvider">The time provider used for delays.</param>
    protected ScopedPeriodicBackgroundService(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
        TimeProvider timeProvider)
    {
        _logger = Guard.AgainstNull(logger);
        _scopeFactory = Guard.AgainstNull(scopeFactory);
        _timeProvider = Guard.AgainstNull(timeProvider);
        _settings = LoopSettings.Snapshot(Guard.AgainstNull(options), GetType());
    }

    /// <summary>
    /// Gets the logical operation name used in structured log messages.
    /// </summary>
    protected virtual string OperationName => GetType().Name;

    /// <inheritdoc />
    protected sealed override Task ExecuteAsync(CancellationToken stoppingToken) =>
        PeriodicLoop.RunAsync(
            _settings,
            _timeProvider,
            _logger,
            OperationName,
            PeriodicBackgroundServiceOptionNames.For(GetType()),
            RunScopedIterationAsync,
            stoppingToken);

    /// <summary>
    /// Executes one unit of background work using services resolved from the current iteration scope.
    /// </summary>
    /// <param name="scopedServices">The service provider for the current iteration scope.</param>
    /// <param name="cancellationToken">A token that is canceled when the host is stopping.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);

    private async Task RunScopedIterationAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await ExecuteIterationAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
