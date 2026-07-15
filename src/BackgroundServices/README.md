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

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddAtyaBackgroundService<InboxWorker>(options =>
        {
            options.Interval = TimeSpan.FromSeconds(30);
            options.FailureDelay = TimeSpan.FromSeconds(5);
            options.MaxConsecutiveFailures = 3;
        });
    })
    .Build();

await host.RunAsync();

internal sealed class InboxWorker(ILogger<InboxWorker> logger)
    : PeriodicBackgroundService(logger)
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

Derive from `ScopedPeriodicBackgroundService` when the worker needs scoped services. Each iteration receives an `IServiceProvider` from a fresh scope, and that scope is disposed after the iteration completes.

```csharp
internal sealed class ScopedInboxWorker(
    ILogger<ScopedInboxWorker> logger,
    IServiceScopeFactory scopeFactory)
    : ScopedPeriodicBackgroundService(logger, scopeFactory)
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
