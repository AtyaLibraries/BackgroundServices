using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class ScopedPeriodicBackgroundServiceTests
{
    [Fact]
    public async Task No_Scoped_Service_Is_Alive_During_A_Delay()
    {
        await using var rig = new Rig();
        ConcurrentQueue<string> events = rig.Time.Events;
        rig.Services.AddScoped(_ => new SyncProbe(events));
        Script<TagA> script = rig.AddScript<TagA>();
        script.ScopedBody = (_, services, _) =>
        {
            services.GetRequiredService<SyncProbe>();
            return Task.CompletedTask;
        };
        rig.Services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>(o => o.Interval = TimeSpan.FromSeconds(1));

        await rig.App.StartAsync();
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => script.Count >= 4);
        await rig.App.StopAsync();

        const int iterations = 3;
        string[] observed = events.Take(iterations * 3).ToArray();
        observed.Should().Equal(Enumerable.Repeat(new[] { "Constructed", "Disposed", "DelayStarted" }, iterations).SelectMany(x => x));
        events.Count(e => e == "Constructed").Should().Be(events.Count(e => e == "Disposed"));
    }

    [Fact]
    public async Task Scope_Is_Disposed_Asynchronously_And_Awaited()
    {
        await using var rig = new Rig();
        var probe = new AsyncProbe();
        rig.Services.AddScoped(_ => probe);
        Script<TagA> script = rig.AddScript<TagA>();
        script.ScopedBody = (_, services, _) =>
        {
            services.GetRequiredService<AsyncProbe>();
            return Task.CompletedTask;
        };
        rig.Services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>(o => o.Interval = TimeSpan.FromMinutes(1));

        await rig.App.StartAsync();
        await rig.WaitUntilAsync(() => rig.Time.TimerCount >= 1);

        probe.DisposeCompleted.Should().BeTrue("the delay only starts after DisposeAsync has been awaited");
        await rig.App.StopAsync();
    }

    [Fact]
    public async Task Each_Iteration_Gets_Its_Own_Scope_Including_Failed_Ones()
    {
        await using var rig = new Rig();
        ConcurrentQueue<string> events = rig.Time.Events;
        rig.Services.AddScoped(_ => new SyncProbe(events));
        Script<TagA> script = rig.AddScript<TagA>();
        script.ScopedBody = (n, services, _) =>
        {
            services.GetRequiredService<SyncProbe>();
            return n == 1 ? throw new InvalidOperationException("first fails") : Task.CompletedTask;
        };
        rig.Services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>(o =>
        {
            o.Interval = TimeSpan.FromSeconds(1);
            o.FailureDelay = TimeSpan.FromSeconds(1);
        });

        await rig.App.StartAsync();
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => script.Count >= 3);
        await rig.App.StopAsync();

        events.Count(e => e == "Constructed").Should().BeGreaterThanOrEqualTo(3);
        events.Count(e => e == "Disposed").Should().Be(events.Count(e => e == "Constructed"));
    }

    [Fact]
    public async Task Shutdown_During_A_Scoped_Iteration_Is_Clean()
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.ScopedBody = async (_, _, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        rig.Services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>();

        await rig.App.StartAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        ScopedScriptedWorker<TagA> worker = rig.Resolve<ScopedScriptedWorker<TagA>>();
        await rig.App.StopAsync();

        worker.Execution!.IsCompletedSuccessfully.Should().BeTrue();
        rig.Log.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Scoped_Worker_Rethrows_After_The_Failure_Limit()
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        script.ScopedBody = (_, _, _) => throw new InvalidOperationException("always");
        rig.Services.AddAtyaScopedBackgroundService<ScopedScriptedWorker<TagA>>(o =>
        {
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = 2;
        });
        ScopedScriptedWorker<TagA> worker = ActivatorUtilities.CreateInstance<ScopedScriptedWorker<TagA>>(rig.App.Services);

        await worker.StartAsync(CancellationToken.None);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => worker.Execution!.IsCompleted);

        script.Count.Should().Be(3);
        worker.Execution!.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public void Constructor_With_Null_ScopeFactory_Throws()
    {
        var rig = new Rig();
        Microsoft.Extensions.Options.IOptionsMonitor<PeriodicBackgroundServiceOptions> monitor =
            rig.App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<PeriodicBackgroundServiceOptions>>();

        Action action = () => _ = new ScopedScriptedWorker<TagA>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScopedScriptedWorker<TagA>>.Instance,
            null!,
            monitor,
            rig.Time,
            new Script<TagA>());

        action.Should().Throw<ArgumentNullException>().WithParameterName("scopeFactory");
    }

    private sealed class SyncProbe : IDisposable
    {
        private readonly ConcurrentQueue<string> _events;

        public SyncProbe(ConcurrentQueue<string> events)
        {
            _events = events;
            _events.Enqueue("Constructed");
        }

        public void Dispose() => _events.Enqueue("Disposed");
    }

    private sealed class AsyncProbe : IAsyncDisposable
    {
        public bool DisposeCompleted { get; private set; }

        public async ValueTask DisposeAsync()
        {
            await Task.Delay(20);
            DisposeCompleted = true;
        }
    }
}
