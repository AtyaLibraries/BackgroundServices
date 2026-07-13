using Atya.Diagnostics.Logging.Extensions;
using Atya.Foundation.Guards;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atya.Hosting.BackgroundServices;

/// <summary>
/// Provides a <see cref="BackgroundService"/> base class for repeatable units of work.
/// </summary>
public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly PeriodicBackgroundServiceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicBackgroundService"/> class.
    /// </summary>
    /// <param name="logger">The logger used to write lifecycle and failure events.</param>
    /// <param name="options">The execution options for the service.</param>
    protected PeriodicBackgroundService(ILogger logger, PeriodicBackgroundServiceOptions? options = null)
    {
        _logger = Guard.AgainstNull(logger);
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
    /// Executes one unit of background work.
    /// </summary>
    /// <param name="cancellationToken">A token that is canceled when the host is stopping.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteIterationAsync(CancellationToken cancellationToken);

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
                await ExecuteIterationAsync(stoppingToken).ConfigureAwait(false);
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
