# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Pack Commands

```bash
# Build
dotnet build Metriquee.sln

# Build release (also generates the NuGet package via GeneratePackageOnBuild)
dotnet build Metriquee.sln -c Release

# Pack explicitly
dotnet pack Metriquee.NetCore/Metriquee.NetCore.csproj -c Release

# Push (supply the API key via env/CI secret — never inline it)
dotnet nuget push "Metriquee.NetCore\bin\Release\Metriquee.NetCore.*.nupkg" \
  --api-key "$NUGET_API_KEY" --source <your-nuget-source> --skip-duplicate
```

There are no tests in the repository currently.

## Architecture

This is a .NET 9 NuGet package library (`Metriquee.NetCore`) that provides ASP.NET Core middleware for automatic application monitoring. It tracks HTTP requests, exceptions, runtime metrics (CPU, memory, GC, thread pool), and health status. It is **not** a runnable application — it's consumed by other ASP.NET Core apps.

### Consumer integration point

Consumers call two extension methods in `Extensions/MetriqueeExtensions.cs`:
- `services.AddMetriquee(opts => ...)` — registers services, options, and background hosted services
- `app.UseMetriquee()` — adds the middleware to the pipeline

### Sink abstraction

All collected data flows through `ICollectorSink` (in `Abstractions/`), which has four track methods: `TrackHttpAsync`, `TrackExceptionAsync`, `TrackMetricsAsync`, `TrackHealthAsync`. The default implementation (`Sinks/LoggerCollectorSink`) writes to `ILogger`. Custom sinks (e.g., HTTP-based transport to a central collector) can replace it via DI.

### Data flow

1. **HTTP & Exceptions** — `Middleware/HttpLoggingMiddleware` and `Middleware/ExceptionLoggingMiddleware` intercept every request. They capture request/response bodies (via a `TeeStream` that writes to both the original stream and a memory buffer), measure duration, and catch unhandled exceptions. All data is sent to `ICollectorSink`.
2. **Metrics** — `Services/MetricsCollectorHostedService` runs as a `BackgroundService` on a periodic timer. It reads process CPU, working set, managed heap, GC generation counts, thread pool stats, and requests-per-second (from `Internal/RequestCounters`, which the middleware increments).
3. **Health** — `Services/HealthPublisherHostedService` runs as a `BackgroundService`. When custom checks are registered (`opts.Health.AddChecker<T>(name, intervalSeconds, isEnabled)`), it runs each `IMetriqueeChecker` (in `Checks/`) on its own interval inside a fresh DI scope, keeps the latest result per check name in memory, and publishes them together as one `HealthEvent` (`Category "custom"`, overall `Status` = worst of all checks) every `Health.IntervalSeconds`. A throwing check is recorded as `Unhealthy`. When no checks are registered it falls back to publishing a single `Healthy` `self` status. Checks return a `MetriqueeCheckResult` (`Healthy`/`Degraded`/`Unhealthy` helpers). Checker types are registered in DI as scoped by `AddMetriquee` (discovered by running the `configure` lambda once on a probe `MetriqueeOptions`).

### Configuration

`Options/MetriqueeOptions` is the root config object (the sender sink requires `Sender.ConnectionString`). It composes:
- `HttpOptions` — toggle HTTP logging, body capture predicates, max body size, excluded paths, sensitive headers, masked JSON fields
- `ExceptionOptions` — toggle exception logging, stack trace limits, excluded exception types
- `MetricsOptions` — toggle and interval
- `HealthOptions` — toggle and interval
- `BatchOptions` — batch size, payload limit, flush interval (used by the sender sink: `MaxBatchSize` triggers a flush, `FlushIntervalSeconds` drives the periodic timer, `MaxPayloadSizeMb` splits oversized batches)

### Key conventions

- All models (`Models/`) and internal services are `internal sealed record` / `internal sealed class`
- Public API surface is limited to: `MetriqueeOptions` and its sub-option records, the two extension methods, and the custom-check API in `Checks/` (`IMetriqueeChecker`, `MetriqueeCheckResult`, `MetriqueeHealthStatus`)
- Uses C# primary constructors and file-scoped namespaces throughout
- `ICollectorSink` is registered with `TryAddSingleton`, so a custom sink registered before `AddMetriquee` wins

## Security Note

`RELEASE.MD` previously contained a cleartext NuGet API key in the `dotnet nuget push` command. It
has been replaced with a `$NUGET_API_KEY` placeholder, but the original key is still in git history —
**rotate it** and supply the key via an environment variable or CI secret going forward. Never inline
publish credentials in tracked files.
