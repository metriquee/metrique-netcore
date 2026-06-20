# Metriquee.NetCore

**.NET 9 · ASP.NET Core · MIT**

Drop-in middleware that automatically monitors your ASP.NET Core app. It tracks:

- **HTTP requests** — path, method, status, duration, headers, optional bodies
- **Exceptions** — unhandled exceptions with inner chain and stack trace
- **Runtime metrics** — CPU, memory, GC, thread pool, requests/sec
- **Health** — periodic health status

Collected data flows to a configurable **sink**: write to `ILogger` (default), POST to a remote
collector over HTTP, or both.

---

## Install

```bash
dotnet add package Metriquee.NetCore
```

## Quick start

Two lines — logs everything to `ILogger` with default settings:

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMetriquee();   // 1. register

var app = builder.Build();
app.UseMetriquee();                   // 2. add middleware
app.Run();
```

## Send to a collector

To ship telemetry to a remote collector instead of (or as well as) `ILogger`, enable the sender sink
and paste the **connection string** you got when you registered the app:

```csharp
builder.Services.AddMetriquee(opts =>
{
    opts.Sender.EnableLoggerSink = false;   // turn off local logging
    opts.Sender.EnableSenderSink = true;    // turn on HTTP transport
    opts.Sender.ConnectionString = "https://mq_live_abc123@collector.example.com";
});
```

The connection string combines the collector endpoint and the ingest key in one value
(`scheme://<apiKey>@host[:port][/path]`); the package splits it back into the base URL and key it needs.

Events are batched and sent as JSON to `POST {baseUrl}/api/logs` with an `X-Api-Key` header. A batch
flushes when it reaches `MaxBatchSize`, every `FlushIntervalSeconds`, or on shutdown.

> **Where does the connection string come from?** Register your app in the Metriquee dashboard.

### Sink modes

| `EnableLoggerSink` | `EnableSenderSink` | Result                   |
|--------------------|--------------------|--------------------------|
| `true` (default)   | `false` (default)  | Logger only              |
| `false`            | `true`             | Sender only              |
| `true`             | `true`             | Both                     |
| `false`            | `false`            | No-op (nothing recorded) |

---

## Configuration

All settings live on `MetriqueeOptions`, passed to `AddMetriquee(opts => ...)`.

**Sender** — `opts.Sender`

| Property           | Default | Description                                                                               |
|--------------------|---------|-------------------------------------------------------------------------------------------|
| `EnableLoggerSink` | `true`  | Write events to `ILogger`                                                                 |
| `EnableSenderSink` | `false` | Send events to the collector over HTTP                                                    |
| `ConnectionString` | `""`    | Combined `scheme://<apiKey>@host` — collector endpoint + ingest key (required for sender) |

> `BaseUrl` and `ApiKey` are read-only values derived from `ConnectionString`.

**HTTP** — `opts.Http`

| Property                    | Default                             | Description                                       |
|-----------------------------|-------------------------------------|---------------------------------------------------|
| `IsEnabled`                 | `true`                              | Enable HTTP logging                               |
| `ShouldCaptureRequestBody`  | `false`                             | Capture request bodies                            |
| `ShouldCaptureResponseBody` | `false`                             | Capture response bodies                           |
| `MaxBodyBytes`              | `4096`                              | Max body bytes captured (rest truncated)          |
| `ExcludedPaths`             | empty                               | Path prefixes to skip (case-insensitive)          |
| `SensitiveHeaders`          | `Authorization, Cookie, Set-Cookie` | Header values replaced with `***`                 |
| `MaskedFields`              | `password`                          | JSON field values replaced with `***` (recursive) |

**Exceptions** — `opts.Exceptions`

| Property             | Default | Description                       |
|----------------------|---------|-----------------------------------|
| `IsEnabled`          | `true`  | Enable exception tracking         |
| `IncludeStackTrace`  | `true`  | Include stack traces              |
| `MaxStackTraceLines` | `100`   | Truncate stack traces past this   |
| `ExcludedExceptions` | empty   | Exception type full names to skip |

**Metrics** — `opts.Metrics` · **Health** — `opts.Health`

| Property                  | Default | Description                      |
|---------------------------|---------|----------------------------------|
| `Metrics.IsEnabled`       | `true`  | Enable metrics collection        |
| `Metrics.IntervalSeconds` | `30`    | Seconds between metrics samples  |
| `Health.IsEnabled`        | `true`  | Enable health publishing         |
| `Health.IntervalSeconds`  | `60`    | Seconds between health publishes |

**Batch** (sender sink) — `opts.Batch`

| Property               | Default | Description                      |
|------------------------|---------|----------------------------------|
| `MaxBatchSize`         | `100`   | Events before an automatic flush |
| `MaxPayloadSizeMb`     | `2`     | Split batches larger than this   |
| `FlushIntervalSeconds` | `5`     | Seconds between periodic flushes |

### Full example

```csharp
builder.Services.AddMetriquee(opts =>
{
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ConnectionString = "https://mq_live_abc123@collector.example.com";

    opts.Http.ShouldCaptureRequestBody  = true;
    opts.Http.ShouldCaptureResponseBody = true;
    opts.Http.MaxBodyBytes = 8192;
    opts.Http.ExcludedPaths    = new(StringComparer.OrdinalIgnoreCase) { "/health", "/swagger" };
    opts.Http.SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase) { "Authorization", "X-Api-Key" };
    opts.Http.MaskedFields     = new(StringComparer.OrdinalIgnoreCase) { "password", "token" };

    opts.Exceptions.MaxStackTraceLines = 50;
    opts.Metrics.IntervalSeconds = 15;
    opts.Health.IntervalSeconds  = 30;
});
```

### Via appsettings.json

Bind the options from configuration instead of (or alongside) code:

```json
{
  "Metriquee": {
    "Sender":     { "EnableSenderSink": true, "ConnectionString": "https://mq_live_abc123@collector.example.com" },
    "Http":       { "ShouldCaptureRequestBody": true, "ShouldCaptureResponseBody": true, "MaxBodyBytes": 4096,
                    "ExcludedPaths": ["/health", "/swagger"], "MaskedFields": ["password", "secret"] },
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

---

## Notes

- **Privacy** — sensitive headers and masked JSON fields are replaced with `***` before anything is
  recorded. Bodies are only captured when explicitly enabled, and always truncated to `MaxBodyBytes`.
- **Path exclusion** — requests whose path starts with any `ExcludedPaths` prefix are skipped entirely.
- **Trace ID** — taken from `Activity.Current` (falls back to `HttpContext.TraceIdentifier`) so HTTP
  and exception events correlate.
