using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class PeriodicBackgroundServiceTests
{
    [Fact]
    public async Task RunAsync_When_Iteration_Succeeds_Repeats_Until_Canceled()
    {
        using var cancellation = new CancellationTokenSource();
        int serviceIterations = 0;
        var service = new TestPeriodicService(
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 2,
            },
            async token =>
            {
                if (Interlocked.Increment(ref serviceIterations) == 2)
                {
                    await cancellation.CancelAsync().ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
            });
        await service.RunAsync(cancellation.Token);

        serviceIterations.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_When_Iteration_Fails_Below_Limit_Retries()
    {
        using var cancellation = new CancellationTokenSource();
        int attempts = 0;
        var service = new TestPeriodicService(
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 3,
            },
            async token =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("retry");
                }

                await cancellation.CancelAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            });

        await service.RunAsync(cancellation.Token);

        attempts.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_When_ConsecutiveFailures_Reach_Limit_Rethrows()
    {
        var service = new TestPeriodicService(
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 2,
            },
            _ => throw new InvalidOperationException("fatal"));

        Func<Task> action = () => service.RunAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("fatal");
    }

    [Fact]
    public void Constructor_With_Null_Logger_Throws()
    {
        Action action = () => _ = new TestPeriodicService(null!, PeriodicBackgroundServiceOptions.Default, _ => Task.CompletedTask);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private sealed class TestPeriodicService : PeriodicBackgroundService
    {
        private readonly Func<CancellationToken, Task> _execute;

        public TestPeriodicService(
            PeriodicBackgroundServiceOptions options,
            Func<CancellationToken, Task> execute)
            : this(NullLogger.Instance, options, execute)
        {
        }

        public TestPeriodicService(
            ILogger logger,
            PeriodicBackgroundServiceOptions options,
            Func<CancellationToken, Task> execute)
            : base(logger, options)
        {
            _execute = execute;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }

        protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
        {
            return _execute(cancellationToken);
        }
    }
}
