using Atya.Diagnostics.Logging.Extensions;
using Atya.Foundation.Guards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Provides a <see cref="BackgroundService"/> base class that creates a fresh dependency injection scope for each iteration.
/// </summary>
public abstract class ScopedPeriodicBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly PeriodicBackgroundServiceOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedPeriodicBackgroundService"/> class.
    /// </summary>
    /// <param name="logger">The logger used to write lifecycle and failure events.</param>
    /// <param name="scopeFactory">The factory used to create one dependency injection scope per iteration.</param>
    /// <param name="options">The execution options for the service.</param>
    protected ScopedPeriodicBackgroundService(
        ILogger logger,
        IServiceScopeFactory scopeFactory,
        PeriodicBackgroundServiceOptions? options = null)
    {
        _logger = Guard.AgainstNull(logger);
        _scopeFactory = Guard.AgainstNull(scopeFactory);
        _options = (options ?? PeriodicBackgroundServiceOptions.Default).Validate();
    }

    /// <summary>
    /// Gets the logical operation name used in structured log messages.
    /// </summary>
    protected virtual string OperationName => GetType().Name;

    /// <inheritdoc />
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogOperationStarted(OperationName, GetType().FullName);

        try
        {
            await RunLoopAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            _logger.LogOperationCompleted(OperationName, GetType().FullName);
        }
    }

    /// <summary>
    /// Executes one unit of background work using services resolved from the current iteration scope.
    /// </summary>
    /// <param name="scopedServices">The service provider for the current iteration scope.</param>
    /// <param name="cancellationToken">A token that is canceled when the host is stopping.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return delay == TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                await ExecuteIterationAsync(scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
                consecutiveFailures = 0;
                await DelayAsync(_options.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (consecutiveFailures + 1 < _options.MaxConsecutiveFailures)
            {
                consecutiveFailures++;
                _logger.LogOperationFailed(exception, OperationName, GetType().FullName);
                _logger.LogRetryAttempt(OperationName, consecutiveFailures, _options.MaxConsecutiveFailures);
                await DelayAsync(_options.FailureDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogOperationFailed(exception, OperationName, GetType().FullName);
                throw;
            }
        }
    }
}
