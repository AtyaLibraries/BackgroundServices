using Atya.Hosting.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices.Samples.ConsoleApp;

/// <summary>
/// Runs the sample console application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the sample service registration.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task Main()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Each worker has its own named options, so two workers can differ.
                services.AddAtyaBackgroundService<SampleWorker>(options =>
                {
                    options.Interval = TimeSpan.FromSeconds(30);
                    options.FailureDelay = TimeSpan.FromSeconds(5);
                    options.MaxConsecutiveFailures = 3;
                });
                services.AddAtyaBackgroundService<SlowSampleWorker>(options =>
                {
                    options.Interval = TimeSpan.FromMinutes(10);
                    options.MaxConsecutiveFailures = null; // never stop
                });
            })
            .Build();

        await host.StartAsync().ConfigureAwait(false);
        await host.StopAsync().ConfigureAwait(false);
    }

    private sealed class SampleWorker : PeriodicBackgroundService
    {
        private static readonly Action<ILogger, Exception?> s_iterationCompleted =
            LoggerMessage.Define(LogLevel.Information, new EventId(1000, nameof(SampleWorker)), "Sample background iteration completed.");

        private readonly ILogger<SampleWorker> _logger;

        public SampleWorker(ILogger<SampleWorker> logger, IOptionsMonitor<PeriodicBackgroundServiceOptions> options, TimeProvider timeProvider)
            : base(logger, options, timeProvider)
        {
            _logger = logger;
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            s_iterationCompleted(_logger, null);
            return Task.CompletedTask;
        }
    }

    private sealed class SlowSampleWorker : PeriodicBackgroundService
    {
        public SlowSampleWorker(ILogger<SlowSampleWorker> logger, IOptionsMonitor<PeriodicBackgroundServiceOptions> options, TimeProvider timeProvider)
            : base(logger, options, timeProvider)
        {
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
