using Atya.Foundation.Guards;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Provides a <see cref="BackgroundService"/> base class for repeatable units of work.
/// </summary>
public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly LoopSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicBackgroundService"/> class.
    /// </summary>
    /// <param name="logger">The logger used to write lifecycle and failure events.</param>
    /// <param name="options">
    /// The options monitor. The named instance for this worker's type (see
    /// <see cref="PeriodicBackgroundServiceOptionNames"/>) is read once and copied.
    /// </param>
    /// <param name="timeProvider">The time provider used for delays.</param>
    protected PeriodicBackgroundService(
        ILogger logger,
        IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
        TimeProvider timeProvider)
    {
        _logger = Guard.AgainstNull(logger);
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
            ExecuteIterationAsync,
            stoppingToken);

    /// <summary>
    /// Executes one unit of background work.
    /// </summary>
    /// <param name="cancellationToken">A token that is canceled when the host is stopping.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteIterationAsync(CancellationToken cancellationToken);
}
