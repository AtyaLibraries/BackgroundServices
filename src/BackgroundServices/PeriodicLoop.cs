using System.Globalization;
using Atya.Diagnostics.Logging.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices;

/// <summary>Immutable copy of the options a worker runs with.</summary>
internal readonly struct LoopSettings
{
    public LoopSettings(TimeSpan interval, TimeSpan failureDelay, int? maxConsecutiveFailures)
    {
        Interval = interval;
        FailureDelay = failureDelay;
        MaxConsecutiveFailures = maxConsecutiveFailures;
    }

    public TimeSpan Interval { get; }

    public TimeSpan FailureDelay { get; }

    public int? MaxConsecutiveFailures { get; }

    public static LoopSettings Snapshot(IOptionsMonitor<PeriodicBackgroundServiceOptions> monitor, Type workerType)
    {
        PeriodicBackgroundServiceOptions options = monitor.Get(PeriodicBackgroundServiceOptionNames.For(workerType));
        return new LoopSettings(options.Interval, options.FailureDelay, options.MaxConsecutiveFailures);
    }
}

internal static class PeriodicLoop
{
    private static readonly Action<ILogger, string, Exception?> s_stopped = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(9101, "BackgroundServiceStopped"),
        "Background service {Worker} stopped.");

    private static readonly Action<ILogger, string, Exception?> s_faulted = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(9102, "BackgroundServiceFaulted"),
        "Background service {Worker} faulted.");

    private static readonly Action<ILogger, string, int, string, Exception?> s_retry = LoggerMessage.Define<string, int, string>(
        LogLevel.Warning,
        new EventId(9103, "BackgroundServiceRetry"),
        "Background service {Worker} failed {Failures} time(s) in a row; tolerating up to {Limit}.");

    public static async Task RunAsync(
        LoopSettings settings,
        TimeProvider timeProvider,
        ILogger logger,
        string operationName,
        string workerName,
        Func<CancellationToken, Task> iteration,
        CancellationToken stoppingToken)
    {
        logger.LogOperationStarted(operationName, workerName);

        try
        {
            await LoopAsync(settings, timeProvider, logger, operationName, workerName, iteration, stoppingToken).ConfigureAwait(false);
            s_stopped(logger, workerName, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            s_faulted(logger, workerName, exception);
            throw;
        }
    }

    private static async Task LoopAsync(
        LoopSettings settings,
        TimeProvider timeProvider,
        ILogger logger,
        string operationName,
        string workerName,
        Func<CancellationToken, Task> iteration,
        CancellationToken stoppingToken)
    {
        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            // No await may appear inside a catch clause: the delay is recorded and awaited after the try,
            // so a cancellation during backoff is handled like any other shutdown.
            TimeSpan delay;
            try
            {
                await iteration(stoppingToken).ConfigureAwait(false);
                consecutiveFailures = 0;
                delay = settings.Interval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (settings.MaxConsecutiveFailures is null || consecutiveFailures < settings.MaxConsecutiveFailures)
            {
                consecutiveFailures++;
                logger.LogOperationFailed(exception, operationName, workerName);
                string limit = settings.MaxConsecutiveFailures?.ToString(CultureInfo.InvariantCulture) ?? "unlimited";
                s_retry(logger, workerName, consecutiveFailures, limit, null);
                delay = settings.FailureDelay;
            }
            catch (Exception exception)
            {
                logger.LogOperationFailed(exception, operationName, workerName);
                throw;
            }

            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
