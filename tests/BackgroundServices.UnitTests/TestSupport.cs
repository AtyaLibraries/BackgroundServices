using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Atya.Hosting.BackgroundServices.UnitTests;

internal sealed class TagA;

internal sealed class TagB;

internal sealed class TagC;

internal sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<LogEntry> _entries;

        public CapturingLogger(ConcurrentQueue<LogEntry> entries) => _entries = entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
    }
}

/// <summary>A fake clock that records every delay timer that is created and lets tests append to a shared event list.</summary>
internal sealed class ObservingTimeProvider : FakeTimeProvider
{
    private int _timers;

    public ConcurrentQueue<string> Events { get; } = new();

    public int TimerCount => Volatile.Read(ref _timers);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Events.Enqueue("DelayStarted");
        Interlocked.Increment(ref _timers);
        return base.CreateTimer(callback, state, dueTime, period);
    }
}

internal sealed class Script<TTag>
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public Func<int, CancellationToken, Task> Body { get; set; } = static (_, _) => Task.CompletedTask;

    public Func<int, IServiceProvider, CancellationToken, Task> ScopedBody { get; set; } = static (_, _, _) => Task.CompletedTask;

    public Task Run(CancellationToken cancellationToken) => Body(Interlocked.Increment(ref _count), cancellationToken);

    public Task RunScoped(IServiceProvider services, CancellationToken cancellationToken)
        => ScopedBody(Interlocked.Increment(ref _count), services, cancellationToken);
}

internal sealed class ScriptedWorker<TTag> : PeriodicBackgroundService
{
    private readonly Script<TTag> _script;

    public ScriptedWorker(
        ILogger<ScriptedWorker<TTag>> logger,
        IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
        TimeProvider timeProvider,
        Script<TTag> script)
        : base(logger, options, timeProvider)
    {
        _script = script;
    }

    public Task? Execution => ExecuteTask;

    protected override Task ExecuteIterationAsync(CancellationToken cancellationToken) => _script.Run(cancellationToken);
}

internal sealed class ScopedScriptedWorker<TTag> : ScopedPeriodicBackgroundService
{
    private readonly Script<TTag> _script;

    public ScopedScriptedWorker(
        ILogger<ScopedScriptedWorker<TTag>> logger,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
        TimeProvider timeProvider,
        Script<TTag> script)
        : base(logger, scopeFactory, options, timeProvider)
    {
        _script = script;
    }

    public Task? Execution => ExecuteTask;

    protected override Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
        => _script.RunScoped(scopedServices, cancellationToken);
}

/// <summary>Builds hosts and workers around a fake clock and a capturing logger.</summary>
internal sealed class Rig : IAsyncDisposable
{
    private IHost? _host;

    public Rig()
    {
        Builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        Builder.Services.AddLogging(logging => logging.AddProvider(Log));
        Builder.Services.AddSingleton<TimeProvider>(Time);
    }

    public ObservingTimeProvider Time { get; } = new();

    public CapturingLoggerProvider Log { get; } = new();

    public HostApplicationBuilder Builder { get; }

    public IServiceCollection Services => Builder.Services;

    public IHost App => _host ??= Builder.Build();

    public Script<TTag> AddScript<TTag>()
    {
        var script = new Script<TTag>();
        Services.AddSingleton(script);
        return script;
    }

    public T Resolve<T>()
        where T : class, IHostedService
        => App.Services.GetServices<IHostedService>().OfType<T>().Single();

    /// <summary>Advances the fake clock in steps until the condition holds, yielding to real threads in between.</summary>
    public async Task PumpUntilAsync(TimeSpan step, Func<bool> condition, int timeoutMilliseconds = 15000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMilliseconds)
            {
                throw new TimeoutException("Condition was not reached.");
            }

            Time.Advance(step);
            await Task.Delay(1);
        }
    }

    public async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 15000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMilliseconds)
            {
                throw new TimeoutException("Condition was not reached.");
            }

            await Task.Delay(1);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            if (_host is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
