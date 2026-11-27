using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atya.Hosting.BackgroundServices.UnitTests;

public sealed class PeriodicBackgroundServiceTests
{
    [Fact]
    public async Task Two_Workers_Observe_Their_Own_Options()
    {
        await using var rig = new Rig();
        Script<TagA> fast = rig.AddScript<TagA>();
        Script<TagB> slow = rig.AddScript<TagB>();
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o => o.Interval = TimeSpan.FromSeconds(1));
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagB>>(o => o.Interval = TimeSpan.FromMinutes(10));

        await rig.App.StartAsync();
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => fast.Count >= 5);

        fast.Count.Should().BeGreaterThanOrEqualTo(5);
        slow.Count.Should().Be(1);
        await rig.App.StopAsync();
    }

    [Fact]
    public async Task Two_Workers_Observe_Their_Own_Failure_Limit()
    {
        await using var rig = new Rig();
        Script<TagA> strict = rig.AddScript<TagA>();
        Script<TagB> lenient = rig.AddScript<TagB>();
        strict.Body = (_, _) => throw new InvalidOperationException("strict");
        lenient.Body = (_, _) => throw new InvalidOperationException("lenient");
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = 1;
        });
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagB>>(o =>
        {
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = 4;
        });
        ScriptedWorker<TagA> a = ActivatorUtilities.CreateInstance<ScriptedWorker<TagA>>(rig.App.Services);
        ScriptedWorker<TagB> b = ActivatorUtilities.CreateInstance<ScriptedWorker<TagB>>(rig.App.Services);

        await a.StartAsync(CancellationToken.None);
        await b.StartAsync(CancellationToken.None);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => a.Execution!.IsCompleted && b.Execution!.IsCompleted);

        strict.Count.Should().Be(2);
        lenient.Count.Should().Be(5);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Exactly_N_Consecutive_Failures_Are_Tolerated_And_The_Next_Is_Rethrown(int limit)
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        var failure = new InvalidOperationException("always");
        script.Body = (_, _) => throw failure;
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = limit;
        });
        ScriptedWorker<TagA> worker = ActivatorUtilities.CreateInstance<ScriptedWorker<TagA>>(rig.App.Services);

        await worker.StartAsync(CancellationToken.None);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => worker.Execution!.IsCompleted);

        script.Count.Should().Be(limit + 1);
        worker.Execution!.IsFaulted.Should().BeTrue();
        worker.Execution.Exception!.InnerException.Should().BeSameAs(failure);
        rig.Log.Entries.Should().Contain(e => e.EventId.Id == 9102);
    }

    [Fact]
    public async Task Null_Failure_Limit_Never_Stops()
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        script.Body = (_, _) => throw new InvalidOperationException("always");
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = null;
        });
        ScriptedWorker<TagA> worker = ActivatorUtilities.CreateInstance<ScriptedWorker<TagA>>(rig.App.Services);

        await worker.StartAsync(CancellationToken.None);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => script.Count >= 25);

        worker.Execution!.IsCompleted.Should().BeFalse();
        await worker.StopAsync(CancellationToken.None);
        worker.Execution.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task A_Success_Resets_The_Consecutive_Failure_Count()
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        script.Body = (n, _) => n % 3 == 0 ? Task.CompletedTask : throw new InvalidOperationException("flaky");
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            o.Interval = TimeSpan.FromSeconds(1);
            o.FailureDelay = TimeSpan.FromSeconds(1);
            o.MaxConsecutiveFailures = 2;
        });
        ScriptedWorker<TagA> worker = ActivatorUtilities.CreateInstance<ScriptedWorker<TagA>>(rig.App.Services);

        await worker.StartAsync(CancellationToken.None);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(1), () => script.Count >= 12);

        worker.Execution!.IsCompleted.Should().BeFalse();
        await worker.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("iteration")]
    [InlineData("interval")]
    [InlineData("backoff")]
    public async Task Shutdown_Is_Clean_At_Every_Phase(string phase)
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (phase == "iteration")
        {
            script.Body = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
        }
        else if (phase == "backoff")
        {
            script.Body = (_, _) => throw new InvalidOperationException("boom");
        }

        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            o.Interval = TimeSpan.FromMinutes(1);
            o.FailureDelay = TimeSpan.FromMinutes(1);
        });

        await rig.App.StartAsync();
        if (phase == "iteration")
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        else
        {
            await rig.WaitUntilAsync(() => rig.Time.TimerCount >= 1);
        }

        ScriptedWorker<TagA> worker = rig.Resolve<ScriptedWorker<TagA>>();
        await rig.App.StopAsync();

        worker.Execution!.IsCompletedSuccessfully.Should().BeTrue();
        rig.Log.Entries.Should().NotContain(e => e.EventId.Id == 9102);
        rig.Log.Entries.Count(e => e.Level >= LogLevel.Error).Should().Be(phase == "backoff" ? 1 : 0);
        rig.Log.Entries.Should().Contain(e => e.EventId.Id == 9101);
    }

    [Fact]
    public async Task Mutating_The_Options_After_Construction_Does_Not_Affect_A_Running_Worker()
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        rig.Services.AddOptions<PeriodicBackgroundServiceOptions>(PeriodicBackgroundServiceOptionNames.For<ScriptedWorker<TagA>>())
            .Configure(o => o.Interval = TimeSpan.FromSeconds(10));
        ScriptedWorker<TagA> worker = ActivatorUtilities.CreateInstance<ScriptedWorker<TagA>>(rig.App.Services);
        PeriodicBackgroundServiceOptions shared = rig.App.Services
            .GetRequiredService<IOptionsMonitor<PeriodicBackgroundServiceOptions>>()
            .Get(PeriodicBackgroundServiceOptionNames.For<ScriptedWorker<TagA>>());

        shared.Interval = TimeSpan.Zero;
        shared.MaxConsecutiveFailures = 1;
        await worker.StartAsync(CancellationToken.None);
        await rig.WaitUntilAsync(() => rig.Time.TimerCount >= 1);
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(50);

        script.Count.Should().Be(1);
        await rig.PumpUntilAsync(TimeSpan.FromSeconds(10), () => script.Count >= 2);
        await worker.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("zero-interval", "Interval")]
    [InlineData("negative-interval", "Interval")]
    [InlineData("negative-delay", "FailureDelay")]
    [InlineData("zero-failures", "MaxConsecutiveFailures")]
    public async Task Invalid_Options_Fail_Host_Start_Before_Any_Iteration(string scenario, string option)
    {
        await using var rig = new Rig();
        Script<TagA> script = rig.AddScript<TagA>();
        rig.Services.AddAtyaBackgroundService<ScriptedWorker<TagA>>(o =>
        {
            switch (scenario)
            {
                case "zero-interval":
                    o.Interval = TimeSpan.Zero;
                    break;
                case "negative-interval":
                    o.Interval = TimeSpan.FromSeconds(-1);
                    break;
                case "negative-delay":
                    o.FailureDelay = TimeSpan.FromSeconds(-1);
                    break;
                default:
                    o.MaxConsecutiveFailures = 0;
                    break;
            }
        });

        Func<Task> start = () => rig.App.StartAsync();

        OptionsValidationException exception = (await start.Should().ThrowAsync<OptionsValidationException>()).Which;
        exception.Message.Should().Contain(option).And.Contain(typeof(ScriptedWorker<TagA>).FullName!);
        script.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_With_Null_Arguments_Throws()
    {
        var rig = new Rig();
        rig.AddScript<TagA>();
        IOptionsMonitor<PeriodicBackgroundServiceOptions> monitor = rig.App.Services.GetRequiredService<IOptionsMonitor<PeriodicBackgroundServiceOptions>>();
        var script = new Script<TagA>();

        Action noLogger = () => _ = new ScriptedWorker<TagA>(null!, monitor, rig.Time, script);
        Action noOptions = () => _ = new ScriptedWorker<TagA>(Microsoft.Extensions.Logging.Abstractions.NullLogger<ScriptedWorker<TagA>>.Instance, null!, rig.Time, script);
        Action noTime = () => _ = new ScriptedWorker<TagA>(Microsoft.Extensions.Logging.Abstractions.NullLogger<ScriptedWorker<TagA>>.Instance, monitor, null!, script);

        noLogger.Should().Throw<ArgumentNullException>();
        noOptions.Should().Throw<ArgumentNullException>();
        noTime.Should().Throw<ArgumentNullException>();
    }
}
