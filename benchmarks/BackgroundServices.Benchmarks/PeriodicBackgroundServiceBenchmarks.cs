using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices.Benchmarks;

/// <summary>
/// Benchmarks the periodic background service success path.
/// </summary>
[MemoryDiagnoser]
public class PeriodicBackgroundServiceBenchmarks : IDisposable
{
    private BenchmarkService _service = null!;

    /// <summary>
    /// Builds benchmark fixtures.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _service = new BenchmarkService();
    }

    /// <summary>
    /// Runs one successful iteration through the public base class.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Benchmark]
    public Task RunSuccessfulIteration()
    {
        using var cancellation = new CancellationTokenSource();
        _service.Cancellation = cancellation;
        return _service.RunAsync(cancellation.Token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _service.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class BenchmarkService : PeriodicBackgroundService
    {
        public CancellationTokenSource? Cancellation { get; set; }

        public BenchmarkService()
            : base(NullLogger.Instance, CreateMonitor(), TimeProvider.System)
        {
        }

        private static IOptionsMonitor<PeriodicBackgroundServiceOptions> CreateMonitor()
        {
            var services = new ServiceCollection();
            services.AddOptions<PeriodicBackgroundServiceOptions>(PeriodicBackgroundServiceOptionNames.For<BenchmarkService>())
                .Configure(options =>
                {
                    options.Interval = TimeSpan.FromHours(1);
                    options.FailureDelay = TimeSpan.Zero;
                    options.MaxConsecutiveFailures = 1;
                });
            return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<PeriodicBackgroundServiceOptions>>();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            Cancellation?.Cancel();
            return Task.CompletedTask;
        }
    }
}
