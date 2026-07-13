using Atya.Hosting.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                services.AddAtyaBackgroundService<SampleWorker>(options =>
                {
                    options.Interval = TimeSpan.FromSeconds(30);
                    options.FailureDelay = TimeSpan.FromSeconds(5);
                    options.MaxConsecutiveFailures = 3;
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

        public SampleWorker(ILogger<SampleWorker> logger)
            : base(logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            s_iterationCompleted(_logger, null);
            return Task.CompletedTask;
        }
    }
}
