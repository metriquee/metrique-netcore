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

Consumers call two extension methods in `Extensions/LogCollectorExtensions.cs`:
- `services.AddLogCollector(opts => ...)` — registers services, options, and background hosted services
- `app.UseLogCollector()` — adds the middleware to the pipeline

### Sink abstraction

All collected data flows through `ILogCollectorSink` (in `Abstractions/`), which has four track methods: `TrackHttpAsync`, `TrackExceptionAsync`, `TrackMetricsAsync`, `TrackHealthAsync`. The default implementation (`Sinks/LoggerLogCollectorSink`) writes to `ILogger`. Custom sinks (e.g., HTTP-based transport to a central collector) can replace it via DI.

### Data flow

1. **HTTP & Exceptions** — `Middleware/LogCollectorMiddleware` intercepts every request. It captures request/response bodies (via a `TeeStream` that writes to both the original stream and a memory buffer), measures duration, and catches unhandled exceptions. All data is sent to `ILogCollectorSink`.
2. **Metrics** — `Services/MetricsCollectorHostedService` runs as a `BackgroundService` on a periodic timer. It reads process CPU, working set, managed heap, GC generation counts, thread pool stats, and requests-per-second (from `Internal/RequestCounters`, which the middleware increments).
3. **Health** — `Services/HealthPublisherHostedService` runs as a `BackgroundService` and periodically publishes health status to the sink.

### Configuration

`Options/LogCollectorOptions` is the root config object (requires `ApiKey`). It composes:
- `HttpOptions` — toggle HTTP logging, body capture predicates, max body size, excluded paths, sensitive headers, masked JSON fields
- `ExceptionOptions` — toggle exception logging, stack trace limits, excluded exception types
- `MetricsOptions` — toggle and interval
- `HealthOptions` — toggle and interval
- `BatchOptions` — batch size, payload limit, flush interval (used by the sender sink: `MaxBatchSize` triggers a flush, `FlushIntervalSeconds` drives the periodic timer, `MaxPayloadSizeMb` splits oversized batches)

### Key conventions

- All models (`Models/`) and internal services are `internal sealed record` / `internal sealed class`
- Public API surface is limited to: `LogCollectorOptions` and its sub-option records, plus the two extension methods
- Uses C# primary constructors and file-scoped namespaces throughout
- `ILogCollectorSink` is registered with `TryAddSingleton`, so consumers can register their own implementation before calling `AddLogCollector`

## Security Note

`RELEASE.MD` previously contained a cleartext NuGet API key in the `dotnet nuget push` command. It
has been replaced with a `$NUGET_API_KEY` placeholder, but the original key is still in git history —
**rotate it** and supply the key via an environment variable or CI secret going forward. Never inline
publish credentials in tracked files.
