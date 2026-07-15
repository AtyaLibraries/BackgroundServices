using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class ScopedPeriodicBackgroundServiceTests
{
    [Fact]
    public async Task RunAsync_Creates_And_Disposes_Fresh_Scope_Per_Iteration()
    {
        TrackedScopedService.Clear();
        var services = new ServiceCollection();
        services.AddScoped<TrackedScopedService>();
        using ServiceProvider provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var resolvedIds = new List<Guid>();
        var service = new TestScopedPeriodicService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 2,
            },
            async (scopedServices, token) =>
            {
                var scopedService = scopedServices.GetRequiredService<TrackedScopedService>();
                resolvedIds.Add(scopedService.Id);

                if (resolvedIds.Count == 2)
                {
                    await cancellation.CancelAsync().ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
            });

        await service.RunAsync(cancellation.Token);

        resolvedIds.Should().HaveCount(2)
            .And.OnlyHaveUniqueItems();
        TrackedScopedService.DisposedIds.Should().BeEquivalentTo(resolvedIds);
    }

    [Fact]
    public void Constructor_With_Null_ScopeFactory_Throws()
    {
        Action action = () => _ = new TestScopedPeriodicService(
            null!,
            PeriodicBackgroundServiceOptions.Default,
            (_, _) => Task.CompletedTask);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("scopeFactory");
    }

    [Fact]
    public async Task RunAsync_When_Iteration_Fails_Below_Limit_Retries()
    {
        var services = new ServiceCollection();
        using ServiceProvider provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        int attempts = 0;
        var service = new TestScopedPeriodicService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 3,
            },
            async (_, token) =>
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
        var services = new ServiceCollection();
        using ServiceProvider provider = services.BuildServiceProvider();
        var service = new TestScopedPeriodicService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PeriodicBackgroundServiceOptions
            {
                Interval = TimeSpan.Zero,
                FailureDelay = TimeSpan.Zero,
                MaxConsecutiveFailures = 2,
            },
            (_, _) => throw new InvalidOperationException("fatal"));

        Func<Task> action = () => service.RunAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("fatal");
    }

    [Fact]
    public void Constructor_With_Null_Logger_Throws()
    {
        var services = new ServiceCollection();
        using ServiceProvider provider = services.BuildServiceProvider();

        Action action = () => _ = new TestScopedPeriodicService(
            null!,
            provider.GetRequiredService<IServiceScopeFactory>(),
            PeriodicBackgroundServiceOptions.Default,
            (_, _) => Task.CompletedTask);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private sealed class TestScopedPeriodicService : ScopedPeriodicBackgroundService
    {
        private readonly Func<IServiceProvider, CancellationToken, Task> _execute;

        public TestScopedPeriodicService(
            IServiceScopeFactory scopeFactory,
            PeriodicBackgroundServiceOptions options,
            Func<IServiceProvider, CancellationToken, Task> execute)
            : this(NullLogger.Instance, scopeFactory, options, execute)
        {
        }

        public TestScopedPeriodicService(
            ILogger logger,
            IServiceScopeFactory scopeFactory,
            PeriodicBackgroundServiceOptions options,
            Func<IServiceProvider, CancellationToken, Task> execute)
            : base(logger, scopeFactory, options)
        {
            _execute = execute;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }

        protected override Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
        {
            return _execute(scopedServices, cancellationToken);
        }
    }

    private sealed class TrackedScopedService : IDisposable
    {
        private static readonly List<Guid> s_disposedIds = [];

        public static IReadOnlyCollection<Guid> DisposedIds => s_disposedIds;

        public Guid Id { get; } = Guid.NewGuid();

        public static void Clear()
        {
            s_disposedIds.Clear();
        }

        public void Dispose()
        {
            s_disposedIds.Add(Id);
        }
    }
}
