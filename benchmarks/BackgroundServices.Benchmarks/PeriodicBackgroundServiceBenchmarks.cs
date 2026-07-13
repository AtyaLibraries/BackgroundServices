using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;

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
            : base(
                NullLogger.Instance,
                new PeriodicBackgroundServiceOptions
                {
                    Interval = TimeSpan.Zero,
                    FailureDelay = TimeSpan.Zero,
                    MaxConsecutiveFailures = 1,
                })
        {
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
