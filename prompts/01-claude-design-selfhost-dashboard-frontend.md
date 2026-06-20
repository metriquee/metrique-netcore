# Prompt — Design the Self-Hosted Observability Dashboard (front end)

> **Paste this whole file into a fresh Claude (design) session.** It is self-contained.
> Audience for the work: developers/SREs who self-host Metriquee inside their own infra.

---

## Your role

Act as a senior product designer + front-end engineer. Produce a **complete, high-fidelity
design** for the **self-hosted Metriquee dashboard** — the web UI that an engineer running
their own Metriquee collector uses to explore the telemetry their applications send in.
Deliver a design system **and** interactive, high-fidelity mockups of the key screens
(HTML/CSS prototype is preferred over static images so flows can be clicked through).

This is an internal operator tool, not a marketing site. Optimize for **information density,
fast scanning, and debugging speed** — think Grafana / Sentry / Datadog APM, not a landing page.

## Product context (what Metriquee is)

Metriquee is an application-monitoring product for .NET (ASP.NET Core) apps. A NuGet agent
(`Metriquee.NetCore`) is dropped into a customer's app; it automatically captures four kinds
of telemetry and ships them in batches to a **collector** over HTTP. In the **self-hosted**
deployment, the operator runs that collector + a database + **this dashboard** on their own
servers. One self-hosted instance may receive data from **many applications**, each identified
by an **API key** (a "project"/"app").

The dashboard reads from the collector's query API (designed separately — assume a normal
REST/JSON backend exists; invent reasonable endpoint names and note your assumptions). The
four telemetry types and their exact field shapes are below — **design around these fields, do
not invent metrics that the agent doesn't actually send.**

### Telemetry the agent sends (authoritative field list, JSON is camelCase)

**HTTP event** — one per request:
```
timestamp, path, method, statusCode, durationMs,
headers: { "Header-Name": ["value", ...] },   // sensitive ones already masked to "***"
requestSizeBytes?, responseSizeBytes?,
requestBody?, responseBody?,                   // optional, truncated, JSON fields masked
traceId
```

**Exception event** — one per unhandled exception:
```
timestamp, type, message, stackTrace,
innerException?: { type, message, stackTrace?, innerException? },   // recursive
traceId, path?, method?
```

**Metrics event** — sampled on an interval (default 30s) per app instance:
```
timestamp,
cpuProcessPercent?, workingSetBytes?, managedHeapBytes?,
gen0Collections, gen1Collections, gen2Collections,
threadPoolAvailableWorkerThreads, threadPoolAvailableIoThreads,
threadPoolMaxWorkerThreads, threadPoolMaxIoThreads,
requestsPerSecond?
```

**Health event** — published on an interval (default 60s):
```
timestamp, category ("self" | "dependencies"),
status ("Healthy" | "Degraded" | "Unhealthy"),
entries: { "name": { status, description?, exceptionMessage?, data?, duration } }
```

`traceId` correlates an HTTP event with any exception thrown during the same request — make
that correlation a first-class navigation in the UI.

## Screens to design (minimum)

1. **Overview / app home** — for a selected app + time range: request volume & RPS sparkline,
   error rate, p50/p95/p99 latency, current health status, CPU & memory trend, recent
   exceptions. Big-number tiles + trend charts. This is the landing screen.
2. **HTTP requests explorer** — filterable, sortable table of HTTP events (by path, method,
   status class 2xx/3xx/4xx/5xx, latency, time). Row → **request detail** drawer/page showing
   headers, sizes, request/response bodies (with masked fields visibly indicated), duration,
   and a link to the correlated exception via `traceId`.
3. **Endpoints / routes view** — aggregated per route+method: request count, error %, latency
   percentiles, throughput. Sortable to find the slowest / most error-prone endpoints.
4. **Exceptions** — grouped by `type` + `message` (issue-style list à la Sentry): occurrence
   count, first/last seen, affected endpoints. Detail view: full stack trace (monospace,
   collapsible frames), nested inner-exception chain, the correlated HTTP request.
5. **Runtime metrics** — time-series charts for CPU %, working set, managed heap, GC
   collections by generation (gen0/1/2), thread-pool utilization (available vs max), RPS.
6. **Health** — current status banner + history timeline; expandable per-entry detail
   (status, description, duration, exception message, data dictionary).
7. **Apps / projects list + API keys** — list of apps reporting in, last-seen, status; create/
   rotate/revoke API keys; per-app retention/ingest settings.
8. **Settings** — data retention, masking config mirror (which headers/JSON fields are masked),
   collector connection info, user management (self-hosted, so simple local auth/roles).

## UX requirements

- **Global controls** persistent across screens: **app/project switcher**, **time-range
  picker** (last 15m / 1h / 6h / 24h / 7d / custom), auto-refresh toggle, and a global search
  by `traceId`.
- **Cross-linking by `traceId`** everywhere (request ⇄ exception).
- Latency shown as **percentiles**, not just averages.
- Make **truncated bodies** and **masked fields/headers** visually obvious (badge/inline note),
  since the agent caps body size and masks secrets before sending.
- Empty states (no app selected, no data in range, agent not reporting yet) and loading
  skeletons for every data view.
- Error/degraded states surfaced prominently (red/amber), healthy states calm.

## Design system to define

- Color tokens for light **and** dark theme (dark is the default for an ops tool); semantic
  status colors for 2xx/4xx/5xx and Healthy/Degraded/Unhealthy.
- Typography scale (UI sans + a monospace for traces, bodies, headers).
- Spacing, radius, elevation tokens; data-dense table, chart, drawer, tile, badge, and
  filter-bar components.
- Charts: line/area for time series, bar for distributions, sparklines for tiles. Specify how
  they read in both themes.
- Responsive: desktop-first (this is a console), but gracefully usable down to tablet width.
- Accessibility: WCAG AA contrast, keyboard-navigable tables/filters, don't encode status by
  color alone (use icon/label too).

## Deliverables

1. A short **design rationale** (layout model, navigation, density choices).
2. The **design system / tokens** (colors, type, spacing, components) as a reference.
3. **High-fidelity, interactive mockups** of at least screens 1–6 above (clickable HTML/CSS
   prototype preferred), in **dark theme**, with at least the Overview and one detail view also
   shown in light theme.
4. Realistic **sample data** in the mockups derived from the field lists above (don't show
   fields the agent never sends).
5. A list of **assumptions** you made about the backend query API (endpoint names, filters,
   aggregation) so the API can be built to match.

## Constraints

- Don't design features the telemetry can't support (e.g., no distributed-trace flame graphs —
  the agent sends a single `traceId` string per request, not span trees).
- Keep it framework-agnostic in spirit, but you may prototype in plain HTML/CSS (+ a charting
  approach of your choice). Note any component library you'd recommend for the real build.
- This is the **self-hosted** product: assume a single tenant/org, simple local auth, and that
  the operator is technical.

Start by proposing the information architecture and navigation, then the design system, then
build out the screens.
