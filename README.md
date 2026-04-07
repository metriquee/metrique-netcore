# Metriquee.NetCore Documentation

**Version:** 1.0.2
**Target Framework:** .NET 9
**License:** MIT

Metriquee.NetCore is an ASP.NET Core middleware library that provides automatic application monitoring. It tracks HTTP
requests, unhandled exceptions, runtime metrics (CPU, memory, GC, thread pool), and health status — then delivers all
collected data through a configurable sink pipeline.

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
    - [LogCollectorOptions](#logcollectoroptions)
    - [SenderOptions](#senderoptions)
    - [HttpOptions](#httpoptions)
    - [ExceptionOptions](#exceptionoptions)
    - [MetricsOptions](#metricsoptions)
    - [HealthOptions](#healthoptions)
    - [BatchOptions](#batchoptions)
- [Sink Modes](#sink-modes)
    - [Logger Sink (Default)](#logger-sink-default)
    - [Sender Sink (HTTP Transport)](#sender-sink-http-transport)
    - [Composite (Both Sinks)](#composite-both-sinks)
- [Features In Detail](#features-in-detail)
    - [HTTP Request/Response Logging](#http-requestresponse-logging)
    - [Exception Tracking](#exception-tracking)
    - [Runtime Metrics Collection](#runtime-metrics-collection)
    - [Health Monitoring](#health-monitoring)
- [Batch Transport Protocol](#batch-transport-protocol)
    - [Endpoint](#endpoint)
    - [BatchPayload Schema](#batchpayload-schema)
- [Examples](#examples)
    - [Minimal Setup](#minimal-setup)
    - [Send to Collector API](#send-to-collector-api)
    - [Full Configuration](#full-configuration)
    - [Full Configuration 2](#full-configuration-2)
    - [Configuration via appsettings.json](#configuration-via-appsettingsjson)

---

## Installation

Install from your configured NuGet source:

```bash
dotnet add package Metriquee.NetCore
```

## Quick Start

Add two lines to your ASP.NET Core application:

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Register services
builder.Services.AddLogCollector();

var app = builder.Build();

// 2. Add middleware
app.UseLogCollector();

app.Run();
```

This enables all features with default settings using the **Logger sink**, which writes all collected events to
`ILogger`.

---

## Configuration

All configuration is done through `LogCollectorOptions`, which is passed to `AddLogCollector()`.

### LogCollectorOptions

Root configuration object. Composes all sub-option groups:

| Property     | Type               | Description                       |
|--------------|--------------------|-----------------------------------|
| `Http`       | `HttpOptions`      | HTTP request/response logging     |
| `Exceptions` | `ExceptionOptions` | Exception tracking                |
| `Metrics`    | `MetricsOptions`   | Runtime metrics collection        |
| `Health`     | `HealthOptions`    | Health status monitoring          |
| `Batch`      | `BatchOptions`     | Batching behavior for sender sink |
| `Sender`     | `SenderOptions`    | Sink selection and HTTP transport |

### SenderOptions

Controls which sinks are active and how the HTTP sender connects to the collector API.

| Property           | Type     | Default | Description                                                  |
|--------------------|----------|---------|--------------------------------------------------------------|
| `EnableLoggerSink` | `bool`   | `true`  | Write events to `ILogger`                                    |
| `EnableSenderSink` | `bool`   | `false` | Send events via HTTP to the collector API                    |
| `ApiKey`           | `string` | `""`    | API key sent in the `X-Api-Key` header (required for sender) |
| `BaseUrl`          | `string` | `""`    | Base URL of the collector API (required for sender)          |

### HttpOptions

Controls HTTP request/response logging behavior.

| Property                    | Type              | Default                                 | Description                                                                      |
|-----------------------------|-------------------|-----------------------------------------|----------------------------------------------------------------------------------|
| `IsEnabled`                 | `bool`            | `true`                                  | Enable/disable HTTP logging entirely                                             |
| `ShouldCaptureRequestBody`  | `bool`            | `false`                                 | Decide if request body is captured                                               |
| `ShouldCaptureResponseBody` | `bool`            | `false`                                 | Decide if response body is captured                                              |
| `MaxBodyBytes`              | `int`             | `4096`                                  | Maximum body size to capture (in bytes). Bodies exceeding this are truncated     |
| `ExcludedPaths`             | `HashSet<string>` | Empty                                   | URL path prefixes to exclude from logging (case-insensitive)                     |
| `SensitiveHeaders`          | `HashSet<string>` | `Authorization`, `Cookie`, `Set-Cookie` | Header names whose values are replaced with `***`                                |
| `MaskedFields`              | `HashSet<string>` | `password`                              | JSON field names in request/response bodies whose values are replaced with `***` |

### ExceptionOptions

Controls exception tracking behavior.

| Property             | Type              | Default | Description                                           |
|----------------------|-------------------|---------|-------------------------------------------------------|
| `IsEnabled`          | `bool`            | `true`  | Enable/disable exception tracking                     |
| `IncludeStackTrace`  | `bool`            | `true`  | Include stack traces in exception events              |
| `MaxStackTraceLines` | `int`             | `100`   | Maximum stack trace lines before truncation           |
| `ExcludedExceptions` | `HashSet<string>` | Empty   | Exception type full names to exclude (case-sensitive) |

### MetricsOptions

Controls periodic runtime metrics collection.

| Property          | Type   | Default | Description                               |
|-------------------|--------|---------|-------------------------------------------|
| `IsEnabled`       | `bool` | `true`  | Enable/disable metrics collection         |
| `IntervalSeconds` | `int`  | `30`    | Seconds between metrics collection cycles |

### HealthOptions

Controls periodic health status publishing.

| Property          | Type   | Default | Description                           |
|-------------------|--------|---------|---------------------------------------|
| `IsEnabled`       | `bool` | `true`  | Enable/disable health monitoring      |
| `IntervalSeconds` | `int`  | `60`    | Seconds between health publish cycles |

### BatchOptions

Controls batching behavior when the sender sink is enabled.

| Property               | Type  | Default | Description                                                |
|------------------------|-------|---------|------------------------------------------------------------|
| `MaxBatchSize`         | `int` | `100`   | Maximum number of events before an automatic flush         |
| `MaxPayloadSizeMb`     | `int` | `2`     | Maximum payload size in MB (batches are split if exceeded) |
| `FlushIntervalSeconds` | `int` | `5`     | Seconds between periodic flushes                           |

---

## Sink Modes

The package supports three sink configurations controlled by `SenderOptions`:

### Logger Sink (Default)

Writes all events to the standard `ILogger` infrastructure. This is the default behavior when no sender options are
configured.

| `EnableLoggerSink` | `EnableSenderSink` | Result      |
|--------------------|--------------------|-------------|
| `true` (default)   | `false` (default)  | Logger only |

Log output examples:

```
info: HTTP GET /api/users 200 45ms traceId=abc123
info: METRICS cpu=2.5% ws=104857600 heap=52428800 rps=15.3
info: HEALTH self status=Healthy entries=0
error: EX System.InvalidOperationException: Something failed traceId=abc123 path=/api/users method=GET
```

### Sender Sink (HTTP Transport)

Batches events and POSTs them as JSON to a remote collector API. Requires `ApiKey` and `BaseUrl`.

| `EnableLoggerSink` | `EnableSenderSink` | Result      |
|--------------------|--------------------|-------------|
| `false`            | `true`             | Sender only |

```csharp
builder.Services.AddLogCollector(opts =>
{
    opts.Sender.EnableLoggerSink = false;
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ApiKey = "your-api-key";
    opts.Sender.BaseUrl = "https://collector.example.com";
});
```

### Composite (Both Sinks)

Runs both sinks simultaneously — events are written to `ILogger` **and** sent via HTTP.

| `EnableLoggerSink` | `EnableSenderSink` | Result |
|--------------------|--------------------|--------|
| `true`             | `true`             | Both   |

```csharp
builder.Services.AddLogCollector(opts =>
{
    opts.Sender.EnableLoggerSink = true;
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ApiKey = "your-api-key";
    opts.Sender.BaseUrl = "https://collector.example.com";
});
```

> **Note:** If both sinks are disabled (`false` / `false`), a no-op composite sink is used and no events are recorded.

---

## Features In Detail

### HTTP Request/Response Logging

The middleware intercepts every HTTP request passing through the ASP.NET Core pipeline and captures:

- **Timestamp** — when the request was processed
- **Path** and **Method** — the request URL path and HTTP method
- **Status Code** — the response status code
- **Duration** — request processing time in milliseconds
- **Headers** — all request headers (sensitive headers are masked)
- **Request/Response Bodies** — captured based on content type predicates, with JSON field masking
- **Size** — request and response body sizes in bytes
- **Trace ID** — from `Activity.Current` or `HttpContext.TraceIdentifier`

**Path exclusion:** Requests matching any prefix in `ExcludedPaths` are skipped entirely (middleware calls `next`
immediately).

**Body capture:** Bodies are only captured when enabled using `ShouldCaptureRequestBody` and `ShouldCaptureResponseBody`

**Header masking:** Headers listed in `SensitiveHeaders` have their values replaced with `***`. By default:
`Authorization`, `Cookie`, `Set-Cookie`.

**JSON field masking:** JSON fields listed in `MaskedFields` have their values replaced with `***` in captured bodies.
This works recursively through nested objects and arrays. By default: `password`.

### Exception Tracking

Unhandled exceptions thrown during request processing are captured with:

- **Type** — full exception type name (e.g., `System.InvalidOperationException`)
- **Message** — the exception message
- **Stack Trace** — optionally included, truncated to `MaxStackTraceLines`
- **Inner Exceptions** — recursively captured with the same detail
- **Trace ID**, **Path**, **Method** — for correlation with the HTTP request

Exceptions listed in `ExcludedExceptions` (by full type name) are not tracked. The exception is always re-thrown after
tracking.

### Runtime Metrics Collection

A background service collects the following metrics at a configurable interval (default: 30 seconds):

| Metric                             | Description                                                     |
|------------------------------------|-----------------------------------------------------------------|
| `CpuProcessPercent`                | Process CPU usage as percentage (normalized by processor count) |
| `WorkingSetBytes`                  | Process working set memory                                      |
| `ManagedHeapBytes`                 | Managed heap size from `GC.GetTotalMemory`                      |
| `Gen0Collections`                  | Garbage collection count for generation 0                       |
| `Gen1Collections`                  | Garbage collection count for generation 1                       |
| `Gen2Collections`                  | Garbage collection count for generation 2                       |
| `ThreadPoolAvailableWorkerThreads` | Available worker threads in the thread pool                     |
| `ThreadPoolAvailableIoThreads`     | Available I/O threads in the thread pool                        |
| `ThreadPoolMaxWorkerThreads`       | Maximum worker threads in the thread pool                       |
| `ThreadPoolMaxIoThreads`           | Maximum I/O threads in the thread pool                          |
| `RequestsPerSecond`                | Calculated from request count since last collection             |

### Health Monitoring

A background service periodically publishes health status (default: every 60 seconds). Each health event includes:

- **Timestamp** — when the health check ran
- **Category** — currently `"self"`
- **Status** — `"Healthy"`, `"Degraded"`, or `"Unhealthy"`
- **Entries** — dictionary of named health check entries, each with status, description, exception message, data, and
  duration

---

## Batch Transport Protocol

When the **sender sink** is enabled, events are batched and sent to the collector API.

### Endpoint

```
POST {BaseUrl}/api/logs
```

**Headers:**

- `X-Api-Key: {ApiKey}` — the configured API key
- `Content-Type: application/json`

**Flush triggers:**

1. The event count reaches `MaxBatchSize` (default: 100)
2. The periodic flush timer fires every `FlushIntervalSeconds` (default: 5 seconds)
3. Application shutdown (graceful drain)

If the serialized JSON exceeds `MaxPayloadSizeMb` (default: 2 MB), the batch is automatically split into smaller
payloads.

### BatchPayload Schema

```json
{
  "apiKey": "string",
  "flushedAt": "2025-01-01T00:00:00Z",
  "httpEvents": [
    {
      "timestamp": "2025-01-01T00:00:00Z",
      "path": "/api/users",
      "method": "GET",
      "statusCode": 200,
      "durationMs": 45,
      "headers": {
        "Content-Type": ["application/json"],
        "Authorization": ["***"]
      },
      "requestSizeBytes": 0,
      "responseSizeBytes": 128,
      "requestBody": null,
      "responseBody": "{\"id\":1}",
      "traceId": "abc123"
    }
  ],
  "exceptionEvents": [
    {
      "timestamp": "2025-01-01T00:00:00Z",
      "type": "System.InvalidOperationException",
      "message": "Something failed",
      "stackTrace": "   at MyApp.Controllers...",
      "innerException": {
        "type": "System.NullReferenceException",
        "message": "Object reference not set",
        "stackTrace": "   at ...",
        "innerException": null
      },
      "traceId": "abc123",
      "path": "/api/users",
      "method": "GET"
    }
  ],
  "metricsEvents": [
    {
      "timestamp": "2025-01-01T00:00:00Z",
      "cpuProcessPercent": 2.5,
      "workingSetBytes": 104857600,
      "managedHeapBytes": 52428800,
      "gen0Collections": 10,
      "gen1Collections": 3,
      "gen2Collections": 1,
      "threadPoolAvailableWorkerThreads": 32760,
      "threadPoolAvailableIoThreads": 1000,
      "threadPoolMaxWorkerThreads": 32767,
      "threadPoolMaxIoThreads": 1000,
      "requestsPerSecond": 15.3
    }
  ],
  "healthEvents": [
    {
      "timestamp": "2025-01-01T00:00:00Z",
      "category": "self",
      "status": "Healthy",
      "entries": {
        "database": {
          "status": "Healthy",
          "description": "Connected",
          "exceptionMessage": null,
          "data": null,
          "duration": "00:00:00.0050000"
        }
      }
    }
  ]
}
```

---

## Examples

### Minimal Setup

Logger sink only, all defaults:

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogCollector();

var app = builder.Build();
app.UseLogCollector();
app.Run();
```

### Send to Collector API

Send events to a remote collector with logger output disabled:

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogCollector(opts =>
{
    opts.Sender.EnableLoggerSink = false;
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ApiKey = "your-api-key";
    opts.Sender.BaseUrl = "https://collector.example.com";
});

var app = builder.Build();
app.UseLogCollector();
app.Run();
```

### Full Configuration

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogCollector(opts =>
{
    // Sink configuration
    opts.Sender.EnableLoggerSink = true;
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ApiKey = "your-api-key";
    opts.Sender.BaseUrl = "https://collector.example.com";

    // HTTP logging
    opts.Http.IsEnabled = true;
    opts.Http.MaxBodyBytes = 8192;
    opts.Http.ShouldCaptureRequestBody = true; 
    opts.Http.ShouldCaptureResponseBody = true;
    opts.Http.ExcludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/metrics",
        "/swagger"
    };
    opts.Http.SensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "X-Api-Key"
    };
    opts.Http.MaskedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "token", "creditCard"
    };

    // Exception tracking
    opts.Exceptions.IsEnabled = true;
    opts.Exceptions.IncludeStackTrace = true;
    opts.Exceptions.MaxStackTraceLines = 50;
    opts.Exceptions.ExcludedExceptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "System.OperationCanceledException",
        "System.Threading.Tasks.TaskCanceledException"
    };

    // Metrics collection
    opts.Metrics.IsEnabled = true;
    opts.Metrics.IntervalSeconds = 15;

    // Health monitoring
    opts.Health.IsEnabled = true;
    opts.Health.IntervalSeconds = 30;

    // Batch settings (for sender sink)
    opts.Batch = new Metriquee.NetCore.Options.BatchOptions
    {
        MaxBatchSize = 200,
        MaxPayloadSizeMb = 4,
        FlushIntervalSeconds = 10
    };
});

var app = builder.Build();
app.UseLogCollector();
app.Run();
```

### Full Configuration 2

```csharp
builder.Services.AddLogCollector(configure: options =>
{
    options.Batch = new BatchOptions
    {
        FlushIntervalSeconds = 20,
        MaxBatchSize = 100,
        MaxPayloadSizeMb = 10,
    };
    options.Exceptions = new ExceptionOptions
    {
        IsEnabled = true,
        MaxStackTraceLines = 70,
    };
    options.Health = new HealthOptions
    {
        IsEnabled = true,
        IntervalSeconds = 3,
    };
    options.Http = new HttpOptions
    {
        IsEnabled = true,
        ShouldCaptureRequestBody = true, 
        ShouldCaptureResponseBody = true,
    };
    options.Metrics = new MetricsOptions
    {
        IsEnabled = true,
        IntervalSeconds = 10,
    };
    options.Sender = new SenderOptions
    {
        EnableLoggerSink = true,
        EnableSenderSink = true,
        ApiKey = "dev-api-key-change-me",
        BaseUrl = "http://localhost:5025",
    };
});
```

### Configuration via appsettings.json

You can bind options from configuration instead of (or in addition to) inline code:

**appsettings.json:**

```json
{
  "LogCollector": {
    "Sender": {
      "EnableLoggerSink": true,
      "EnableSenderSink": true,
      "ApiKey": "your-api-key",
      "BaseUrl": "https://collector.example.com"
    },
    "Http": {
      "IsEnabled": true,
      "MaxBodyBytes": 4096,
      "ShouldCaptureRequestBody": true,
      "ShouldCaptureResponseBody": true,
      "ExcludedPaths": ["/health", "/swagger"],
      "SensitiveHeaders": ["Authorization", "Cookie", "Set-Cookie"],
      "MaskedFields": ["password", "secret"]
    },
    "Exceptions": {
      "IsEnabled": true,
      "IncludeStackTrace": true,
      "MaxStackTraceLines": 100,
      "ExcludedExceptions": ["System.OperationCanceledException"]
    },
    "Metrics": {
      "IsEnabled": true,
      "IntervalSeconds": 30
    },
    "Health": {
      "IsEnabled": true,
      "IntervalSeconds": 60
    },
    "Batch": {
      "MaxBatchSize": 100,
      "MaxPayloadSizeMb": 2,
      "FlushIntervalSeconds": 5
    }
  }
}
```

**Program.cs:**

```csharp
using Metriquee.NetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogCollector();
builder.Services.Configure<Metriquee.NetCore.Options.LogCollectorOptions>(
    builder.Configuration.GetSection("LogCollector"));

var app = builder.Build();
app.UseLogCollector();
app.Run();
```

> **Note:** `ShouldCaptureRequestBody` and `ShouldCaptureResponseBody` are `Func<string, bool>` delegates and cannot be
> configured via JSON. Use inline configuration for these.
