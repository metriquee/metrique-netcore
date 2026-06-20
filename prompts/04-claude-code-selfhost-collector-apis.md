# Prompt — Build the Self-Hosted Collector & Query API (Claude Code)

> **Paste this whole file into a fresh Claude Code session at the root of the new collector repo.**
> It is self-contained. This is the **telemetry ingestion + query service** that a team runs when
> they self-host Metriquee. It is the server side of the existing `Metriquee.NetCore` agent.

---

## Your task

Design and implement the **Metriquee collector service**: the HTTP API that (a) **ingests**
batched telemetry from the `Metriquee.NetCore` agent, persists it, and (b) **serves queries** for
the self-hosted dashboard. Deliver a concrete, buildable API — **endpoint list with methods/paths/
request+response shapes/auth/status codes**, the storage model, and a runnable scaffold. The
ingestion endpoint's wire contract is **already fixed by the shipping agent** and must be matched
exactly; you have design freedom on the query API and storage.

## Authoritative ingestion contract (must match — the agent already sends this)

The agent POSTs batches:

```
POST {BaseUrl}/api/logs
Headers:  X-Api-Key: <the app's API key>
          Content-Type: application/json
Body:     BatchPayload (JSON, camelCase)
```

The agent flushes on a size threshold (default 100 events), on a timer (default every 5s), and on
shutdown; it **splits batches** so each stays under a configurable size (default 2 MB). It expects
a **2xx** on success and will **log an error and drop** on non-2xx (no agent-side retry/queue
today), so ingestion should be lenient, fast, and accept partial validity rather than 500 the
whole batch.

### `BatchPayload`
```jsonc
{
  "apiKey": "string",                 // also present in body; AUTH is the X-Api-Key header
  "flushedAt": "2025-01-01T00:00:00Z",
  "httpEvents":      [HttpEvent...],
  "exceptionEvents": [ExceptionEvent...],
  "metricsEvents":   [MetricsEvent...],
  "healthEvents":    [HealthEvent...]
}
```

### `HttpEvent`
```jsonc
{
  "timestamp": "…", "path": "/api/users", "method": "GET",
  "statusCode": 200, "durationMs": 45,
  "headers": { "Content-Type": ["application/json"], "Authorization": ["***"] },
  "requestSizeBytes": 0, "responseSizeBytes": 128,   // nullable
  "requestBody": null, "responseBody": "{\"id\":1}", // nullable, truncated, masked
  "traceId": "abc123"
}
```

### `ExceptionEvent`
```jsonc
{
  "timestamp": "…", "type": "System.InvalidOperationException",
  "message": "Something failed", "stackTrace": "   at …",
  "innerException": { "type": "…", "message": "…", "stackTrace": "…", "innerException": null },
  "traceId": "abc123", "path": "/api/users", "method": "GET"   // path/method nullable
}
```

### `MetricsEvent`
```jsonc
{
  "timestamp": "…",
  "cpuProcessPercent": 2.5, "workingSetBytes": 104857600, "managedHeapBytes": 52428800,
  "gen0Collections": 10, "gen1Collections": 3, "gen2Collections": 1,
  "threadPoolAvailableWorkerThreads": 32760, "threadPoolAvailableIoThreads": 1000,
  "threadPoolMaxWorkerThreads": 32767, "threadPoolMaxIoThreads": 1000,
  "requestsPerSecond": 15.3
}
```

### `HealthEvent`
```jsonc
{
  "timestamp": "…", "category": "self",            // "self" | "dependencies"
  "status": "Healthy",                              // "Healthy" | "Degraded" | "Unhealthy"
  "entries": { "database": { "status": "Healthy", "description": "Connected",
               "exceptionMessage": null, "data": null, "duration": "00:00:00.0050000" } }
}
```

Notes that affect ingestion: sensitive headers and JSON body fields arrive **already masked**
(`"***"`); bodies are already **truncated** by the agent; `traceId` correlates an `HttpEvent` with
any `ExceptionEvent` from the same request. The `apiKey` is duplicated in the body for legacy
reasons — **authenticate on the `X-Api-Key` header**, treat the body field as informational only.

## Recommended stack

- **.NET 9 / ASP.NET Core Minimal API** (matches the agent; same runtime/team skills).
- Storage: telemetry is **append-heavy time-series**. Default to **PostgreSQL** (optionally
  TimescaleDB hypertables) for a simple self-host; document **ClickHouse** as the scale-out
  option. Use partitioning by time + app and TTL/retention jobs. Justify your choice.
- System.Text.Json (camelCase) to match the agent payload exactly.
- OpenAPI/Swagger, structured logging, EF Core or Dapper (justify), health checks.
- Ship a `docker-compose` (collector + database) so `docker compose up` gives a working instance.

## Storage model (design these)

- **App/tenant** keyed by API key (self-host may be single-org with many apps). Store apps + key
  hashes, or validate against the control-plane API if deployed alongside it — make it pluggable
  (local key table **or** remote validation). Track `last-seen-at`.
- **http_events**, **exception_events**, **metrics_events**, **health_events** — time-partitioned,
  indexed by `(appId, timestamp)`, plus `traceId` index on http/exception for correlation, and a
  `(path, method, statusClass)` index to power the endpoints/aggregation view.
- Pre-aggregation/rollups (per-minute/-hour request counts, error rates, latency percentiles) so
  the dashboard's overview and endpoints views are fast — either continuous aggregates
  (Timescale) or scheduled rollup jobs. Document the approach.
- **Retention**: configurable per-type TTL; a background job that drops/rolls-off old partitions.

## Endpoints to design & implement

### Ingestion (contract fixed above)
- `POST /api/logs` — auth via `X-Api-Key`; validate key → resolve app; parse `BatchPayload`;
  persist each event list. **Be lenient**: skip malformed individual events rather than failing
  the batch; return `202 Accepted` (or `200`) with a small summary
  (`{ accepted, rejected }`). Reject only on missing/invalid key (`401`) or unparseable body
  (`400`). Enforce a max request size and apply backpressure/rate limiting per key. Idempotency:
  document how you handle duplicate batches (the agent has no dedup).

### Query API (for the dashboard — you design these; keep them RESTful and consistent)
All query routes are **operator-authenticated** (dashboard user/session or admin token — NOT the
ingest `X-Api-Key`), scoped by `appId` and a time range (`from`,`to`), with pagination.

- `GET /api/apps` — apps reporting in (id, name, last-seen, current health).
- **Overview**: `GET /api/apps/{appId}/overview?from&to` — request volume, RPS, error rate,
  latency p50/p95/p99, current health, CPU/memory summary (served from rollups).
- **HTTP**:
  - `GET /api/apps/{appId}/http?from&to&path&method&status&minDuration&cursor` — filterable list.
  - `GET /api/apps/{appId}/http/{eventId}` — full detail incl. headers + bodies.
  - `GET /api/apps/{appId}/endpoints?from&to` — aggregated per `path`+`method`: count, error %,
    p50/p95/p99, throughput.
- **Exceptions**:
  - `GET /api/apps/{appId}/exceptions?from&to&type&path&cursor` — **grouped** by `type`+`message`
    with count, first/last seen, affected endpoints (Sentry-style issues list).
  - `GET /api/apps/{appId}/exceptions/{groupId}` — group detail + recent occurrences + inner chain.
- **Trace correlation**: `GET /api/apps/{appId}/traces/{traceId}` — the HTTP event + any
  exceptions sharing that `traceId`.
- **Metrics**: `GET /api/apps/{appId}/metrics?from&to&series=cpu,memory,gc,threadpool,rps&step` —
  time-bucketed series for charts.
- **Health**: `GET /api/apps/{appId}/health?from&to` — current status + history timeline + entries.
- **Settings/admin**: retention config, app key management (if keys are local), and
  `GET /healthz` + `GET /readyz` for the collector itself.

For **every** endpoint specify method, path, auth, query/body, success shape + status, and errors.

## Cross-cutting requirements

- **Performance**: ingestion is the hot path — make it cheap (bulk insert, async, bounded
  channels/queue between accept and persist if helpful). Don't block the agent.
- **Resilience**: a bad event must not poison a batch; a DB hiccup should return a retryable
  status, not crash. Log and meter rejected events.
- **Security**: constant-time API-key compare, store key **hashes** only, enforce request-size
  limits, separate auth for ingest (`X-Api-Key`) vs query (operator). Bodies/headers are
  pre-masked by the agent but treat them as potentially sensitive at rest (document encryption/
  retention posture).
- **Config via env** (DB connection, retention, rate limits, auth) — **no cleartext secrets in
  source or config files** (the existing repo had that problem; do not repeat it).
- **Observability** of the collector itself: structured logs + counters (events ingested,
  rejected, batch size, ingest latency).
- Migrations, seed, `docker-compose`, README, OpenAPI.
- **Tests**: an ingestion test that posts a real `BatchPayload` and asserts persistence + the
  `accepted/rejected` summary; a malformed-event-skip test; a key-auth test; a query test for
  the endpoints aggregation and `traceId` correlation.

## Deliverables

1. A written **API + storage design doc**: ingestion handling (validation/leniency/idempotency),
   the storage schema + partitioning + rollup strategy + retention, and the full query-endpoint
   table — **before** large-scale coding. Confirm the design, then implement.
2. The implemented scaffold: `POST /api/logs` matching the contract exactly, persistence, and at
   least the overview + http list/detail + exceptions + metrics + trace-correlation query
   endpoints end-to-end.
3. `docker-compose` (collector + DB) + migrations + README + OpenAPI.
4. Tests covering ingestion, leniency, auth, and the key query/aggregation paths.
5. A list of assumptions and deviations, with reasons.

## Constraints

- **Match the ingestion contract exactly** — field names are camelCase as shown; do not rename or
  require fields the agent doesn't send; tolerate nullable fields and the legacy body `apiKey`.
- This service ingests + serves telemetry only. Account/billing/team management belongs to the
  separate control-plane API — don't build it here, but make API-key validation pluggable so the
  two can integrate (local key table for standalone self-host, or remote validation when deployed
  with the control plane).
- Keep the dashboard's needs (see its design prompt) in mind so the query shapes line up with what
  the UI renders (overview tiles, requests explorer, endpoints aggregation, exception groups,
  metrics series, health timeline, `traceId` correlation).

Begin by producing the design doc (ingestion + storage + query endpoint table), then scaffold and
implement, starting with `POST /api/logs`.
