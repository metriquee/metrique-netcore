# Metriquee.NetCore

**.NET 9 · ASP.NET Core · MIT**

Drop-in middleware that automatically monitors your ASP.NET Core app:

- **HTTP requests** — path, method, status, duration, headers, optional bodies
- **Exceptions** — unhandled exceptions with inner chain and stack trace
- **Runtime metrics** — CPU, memory, GC, thread pool, requests/sec
- **Health** — periodic status, plus your own custom health checks

Collected data flows to a **sink**: `ILogger` (default), a remote collector over HTTP, or both.

## Install

```bash
dotnet add package Metriquee.NetCore
```

## Quick start

Two lines — logs everything to `ILogger`:

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMetriquee();   // 1. register

var app = builder.Build();
app.UseMetriquee();                // 2. add middleware
app.Run();
```

## Send to a collector

Enable the sender sink and paste the **connection string** you got when you registered the app in the
Metriquee dashboard:

```csharp
builder.Services.AddMetriquee(opts =>
{
    opts.Sender.EnableLoggerSink = false;   // turn off local logging
    opts.Sender.EnableSenderSink = true;    // turn on HTTP transport
    opts.Sender.ConnectionString = "https://mq_live_abc123@collector.example.com";
});
```

| `EnableLoggerSink` | `EnableSenderSink` | Result |
|--------------------|--------------------|--------|
| `true` (default)   | `false` (default)  | Logger only |
| `false`            | `true`             | Sender only |
| `true`             | `true`             | Both |
| `false`            | `false`            | No-op |

## Custom health checks

Implement `IMetriqueeChecker`; each check runs on its own interval and appears in the dashboard with
its status, last-run time, and message. Checkers are resolved from a fresh DI scope per run (inject
scoped services like a `DbContext`), and a throwing check is reported as `Unhealthy` without affecting
the others.

```csharp
using Metriquee.NetCore.Checks;

public sealed class DatabaseHealthCheck(MyDbContext db) : IMetriqueeChecker
{
    public async Task<MetriqueeCheckResult> CheckHealthAsync(CancellationToken ct = default)
        => await db.Database.CanConnectAsync(ct)
            ? MetriqueeCheckResult.Healthy("Database reachable")
            : MetriqueeCheckResult.Unhealthy("Cannot reach database");
}
```

```csharp
builder.Services.AddMetriquee(opts =>
{
    opts.Health.AddChecker<DatabaseHealthCheck>("database", 30);             // runs every 30s
    opts.Health.AddChecker<RedisHealthCheck>("redis", 15, isEnabled: false); // registered but off
});
```

## Configuration

All settings live on `MetriqueeOptions`, passed to `AddMetriquee(opts => ...)` or bound from config.

**Sender** — `opts.Sender`

| Property | Default | Description |
|----------|---------|-------------|
| `EnableLoggerSink` | `true`  | Write events to `ILogger` |
| `EnableSenderSink` | `false` | Send events to the collector over HTTP |
| `ConnectionString` | `""`    | Combined `scheme://<apiKey>@host` — endpoint + key (required for sender) |

**Resource tags** — `opts.Resource`

Stamped onto every batch so telemetry can be filtered by deployment environment and release in the
dashboard. All three auto-default when left blank — set `Release` per deploy for meaningful
release/regression tracking.

| Property | Default | Description |
|----------|---------|-------------|
| `Environment` | `IHostEnvironment.EnvironmentName` (e.g. `Production`) | Deployment environment |
| `Release` | entry assembly informational version | Running app version, e.g. `1.4.2` |
| `Host` | machine name | Host / instance identifier |

```csharp
builder.Services.AddMetriquee(opts =>
{
    opts.Resource.Environment = "Production";
    opts.Resource.Release     = "1.4.2";   // e.g. your CI build / git tag
});
```

**HTTP** — `opts.Http`

| Property | Default | Description |
|----------|---------|-------------|
| `IsEnabled` | `true` | Enable HTTP logging |
| `ShouldCaptureRequestBody` / `ShouldCaptureResponseBody` | `false` | Capture bodies |
| `MaxBodyBytes` | `4096` | Max body bytes captured (rest truncated) |
| `ExcludedPaths` | empty | Path prefixes to skip (case-insensitive) |
| `SensitiveHeaders` | `Authorization, Cookie, Set-Cookie` | Header values replaced with `***` |
| `MaskedFields` | `password` | JSON field values replaced with `***` (recursive) |

**Exceptions** — `opts.Exceptions`

| Property | Default | Description |
|----------|---------|-------------|
| `IsEnabled` | `true` | Enable exception tracking |
| `IncludeStackTrace` | `true` | Include stack traces |
| `MaxStackTraceLines` | `100` | Truncate stack traces past this |
| `ExcludedExceptions` | empty | Exception type full names to skip |

**Metrics / Health / Batch**

| Property | Default | Description |
|----------|---------|-------------|
| `Metrics.IntervalSeconds` | `30` | Seconds between metrics samples |
| `Health.IntervalSeconds` | `60` | Seconds between health publishes |
| `Batch.MaxBatchSize` | `100` | Events before an automatic flush (sender) |
| `Batch.MaxPayloadSizeMb` | `2` | Split batches larger than this |
| `Batch.FlushIntervalSeconds` | `5` | Seconds between periodic flushes |

### Via appsettings.json

```json
{
  "Metriquee": {
    "Sender":     { "EnableSenderSink": true, "ConnectionString": "https://mq_live_abc123@collector.example.com" },
    "Http":       { "ShouldCaptureRequestBody": true, "MaxBodyBytes": 4096, "MaskedFields": ["password", "secret"] },
    "Exceptions": { "MaxStackTraceLines": 100 },
    "Metrics":    { "IntervalSeconds": 30 },
    "Health":     { "IntervalSeconds": 60 }
  }
}
```

```csharp
builder.Services.AddMetriquee();
builder.Services.Configure<Metriquee.NetCore.Options.MetriqueeOptions>(
    builder.Configuration.GetSection("Metriquee"));
```

## Notes

- **Privacy** — sensitive headers and masked JSON fields become `***` before anything is recorded;
  bodies are captured only when enabled and always truncated to `MaxBodyBytes`.
- **Trace ID** — taken from `Activity.Current` (falls back to `HttpContext.TraceIdentifier`) so HTTP
  and exception events correlate.
