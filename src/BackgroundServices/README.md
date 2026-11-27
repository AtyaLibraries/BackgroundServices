# Atya.Hosting.BackgroundServices

Small hosted-service execution helpers for production .NET workers.

## Overview

`Atya.Hosting.BackgroundServices` provides narrow `BackgroundService` base classes for repeatable worker loops. It keeps cancellation, retry delays, consecutive-failure limits, per-iteration dependency injection scopes, and lifecycle logging consistent without introducing a worker framework or scheduler.

## Installation

```bash
dotnet add package Atya.Hosting.BackgroundServices
```

## Quick Start

```csharp
using Atya.Hosting.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddAtyaBackgroundService<InboxWorker>(options =>
        {
            options.Interval = TimeSpan.FromSeconds(30);
            options.FailureDelay = TimeSpan.FromSeconds(5);
            options.MaxConsecutiveFailures = 3; // or null to never stop
        });
    })
    .Build();

await host.RunAsync();

internal sealed class InboxWorker(
    ILogger<InboxWorker> logger,
    IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
    TimeProvider timeProvider)
    : PeriodicBackgroundService(logger, options, timeProvider)
{
    protected override Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

## Feature Tour

### PeriodicBackgroundService

Derive from `PeriodicBackgroundService` and implement one unit of work in `ExecuteIterationAsync`. The base class calls it repeatedly until the host cancellation token is canceled.

### ScopedPeriodicBackgroundService

Derive from `ScopedPeriodicBackgroundService` when the worker needs scoped services. Each iteration receives an `IServiceProvider` from a fresh scope, and that scope is disposed after the iteration completes. This avoids capturing scoped dependencies in the hosted-service singleton.

```csharp
internal sealed class ScopedInboxWorker(
    ILogger<ScopedInboxWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<PeriodicBackgroundServiceOptions> options,
    TimeProvider timeProvider)
    : ScopedPeriodicBackgroundService(logger, scopeFactory, options, timeProvider)
{
    protected override Task ExecuteIterationAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var handler = scopedServices.GetRequiredService<InboxHandler>();
        return handler.ProcessAsync(cancellationToken);
    }
}
```

### PeriodicBackgroundServiceOptions

Configure `Interval`, `FailureDelay`, and `MaxConsecutiveFailures`. A successful iteration resets the consecutive-failure count; reaching the failure limit lets the original exception escape to the host.

### Registration Helpers

Use `AddAtyaBackgroundService<TService>` for `PeriodicBackgroundService` implementations. Use `AddAtyaScopedBackgroundService<TService>` for `ScopedPeriodicBackgroundService` implementations. Both helpers can configure `PeriodicBackgroundServiceOptions`.

## Migrating from 1.x to 2.0

- **Constructors.** Derived workers now pass `IOptionsMonitor<PeriodicBackgroundServiceOptions>` and a `TimeProvider` to the base class (see the examples above). `TimeProvider` is required; the `AddAtya...BackgroundService` helpers register `TimeProvider.System` if none is registered.
- **Options are per worker.** Each worker reads the options named `PeriodicBackgroundServiceOptionNames.For<TWorker>()` (the worker's full type name) once, at construction, and copies them. The `configure` callback of `AddAtyaBackgroundService` now takes effect (in 1.x it was ignored) and two workers can have different settings. Later changes to the options object or a configuration reload do not affect a running worker. To bind configuration use `services.AddOptions<PeriodicBackgroundServiceOptions>(PeriodicBackgroundServiceOptionNames.For<MyWorker>()).BindConfiguration("Workers:MyWorker")`. Shared defaults: `services.ConfigureAll<PeriodicBackgroundServiceOptions>(...)`; the unnamed `Configure<>` does not reach named instances.
- **`PeriodicBackgroundServiceOptions.Default` is removed.** It was a shared mutable instance.
- **Validation at startup.** `Interval` must be greater than zero, `FailureDelay` must not be negative, `MaxConsecutiveFailures` must be `null` or at least 1. Invalid values fail host start and name the worker and the option.
- **`MaxConsecutiveFailures = N` tolerates exactly N failures** (fix: 1.x tolerated N-1, so `1` tolerated none). The default of 3 now tolerates three. `null` (type is now `int?`) tolerates failures forever.
- **Scoped workers** dispose the iteration scope asynchronously, before the interval delay starts, so scoped services (DbContext, connections) are no longer held while idle. Do not keep services from the iteration scope beyond the iteration.
- **Clean shutdown.** Stopping the host during the failure backoff no longer faults `ExecuteAsync`.

## Error Codes

This package does not define error codes. Programmer errors are reported through argument exceptions from guard checks, and worker failures remain the original exceptions thrown by the worker implementation.

## Why These Dependencies

- `Atya.Foundation.Guards` keeps public argument validation consistent across Atya packages.
- `Atya.Diagnostics.Logging` provides shared structured logging helpers.
- `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, and `Microsoft.Extensions.Options` are the platform abstractions this package extends.

## Links

- Repository: https://github.com/AtyaLibraries/BackgroundServices
- NuGet: https://www.nuget.org/packages/Atya.Hosting.BackgroundServices
- Samples: https://github.com/AtyaLibraries/BackgroundServices/tree/development/samples
- License: https://github.com/AtyaLibraries/BackgroundServices/blob/development/LICENSE
