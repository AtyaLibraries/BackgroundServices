<h1 align="center">Atya.Hosting.BackgroundServices</h1>

<p align="center"><i>Small hosted-service execution helpers for production .NET workers.</i></p>

<p align="center">
  <a href="https://www.nuget.org/packages/Atya.Hosting.BackgroundServices"><img src="https://img.shields.io/nuget/v/Atya.Hosting.BackgroundServices?style=for-the-badge&logo=nuget&logoColor=white&label=NuGet&color=512BD4" alt="NuGet Version"></a>
  <a href="https://www.nuget.org/packages/Atya.Hosting.BackgroundServices"><img src="https://img.shields.io/nuget/dt/Atya.Hosting.BackgroundServices?style=for-the-badge&logo=nuget&logoColor=white&label=Downloads&color=512BD4" alt="NuGet Downloads"></a>
  <img src="https://img.shields.io/badge/.NET_10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt="Target Framework">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/AtyaLibraries/BackgroundServices?style=for-the-badge&color=512BD4" alt="License"></a>
  <a href="https://github.com/AtyaLibraries/BackgroundServices/actions"><img src="https://img.shields.io/github/actions/workflow/status/AtyaLibraries/BackgroundServices/ci.yml?branch=development&style=for-the-badge&logo=githubactions&logoColor=white&label=Build" alt="Build"></a>
</p>

---

## Overview

`Atya.Hosting.BackgroundServices` provides a narrow `BackgroundService` base class for repeatable worker loops. It keeps cancellation, retry delays, consecutive-failure limits, and lifecycle logging consistent without introducing a worker framework or scheduler.

Use it when a service already owns the actual work and only needs boring hosting plumbing around each iteration.

## Features

- Periodic execution base class built on `Microsoft.Extensions.Hosting.BackgroundService`.
- Configurable success interval, failure delay, and maximum consecutive failures.
- Structured lifecycle and failure logging through `Atya.Diagnostics.Logging`.
- DI helper for registering hosted services with package-owned options.

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

### Repeat Work Until The Host Stops

Derive from `PeriodicBackgroundService` and implement one unit of work in `ExecuteIterationAsync`. The base class calls it repeatedly until the host cancellation token is canceled.

### Bound Consecutive Failures

`MaxConsecutiveFailures` controls when the base class stops retrying and lets the exception escape to the host. A successful iteration resets the failure count.

### Keep Delays Explicit

`Interval` is used after successful work. `FailureDelay` is used after retryable failures. Set either to `TimeSpan.Zero` for tests or very tight worker loops.

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

## License

Released under the MIT license. See [LICENSE](LICENSE).
