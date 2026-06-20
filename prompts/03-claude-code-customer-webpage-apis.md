# Prompt — Build the Customer Website / SaaS Control-Plane API (Claude Code)

> **Paste this whole file into a fresh Claude Code session at the root of the new API repo.**
> It is self-contained. This is the **control plane** behind the public website + account portal,
> NOT the telemetry-ingestion collector (that is a separate service — see the self-host API prompt).

---

## Your task

Design and implement the **backend API for the Metriquee customer website and account portal**:
authentication, organizations/teams, monitored apps, API-key lifecycle, plans/billing, and
usage. Deliver a concrete, buildable API — **endpoint list with methods/paths/request+response
shapes/auth/status codes**, the data model, and a runnable scaffold. Treat this as the system of
record for **accounts and API keys**; the telemetry data itself lives in the collector service.

## Product context

Metriquee is application monitoring for .NET apps. Customers sign up on the website, create an
**app** (a monitored application), and receive an **API key**. They drop the `Metriquee.NetCore`
NuGet agent into their ASP.NET Core app, paste the key, and the app starts shipping telemetry to
the **collector** (`POST {collector}/api/logs`, authenticated by that same `X-Api-Key`). This
control-plane API owns everything *except* the telemetry: who the user is, what apps/keys exist,
what plan they're on, and how much they've used. The collector validates incoming API keys
against keys this service issues — so define a clean contract for that (see "Integration").

## Recommended stack

- **.NET 9 / ASP.NET Core** (Minimal API or controllers) to stay consistent with the rest of
  Metriquee (the agent is .NET 9). EF Core + **PostgreSQL** for the relational store.
- JWT (access + refresh) or cookie-based sessions for portal auth; choose one and justify it.
- API keys: generate a high-entropy token, **store only a hash** (e.g. SHA-256) plus a short
  non-secret prefix for display/lookup; show the full key to the user exactly once.
- OpenAPI/Swagger generated from the implementation. Structured logging. FluentValidation or
  data annotations for request validation.
- You may pick a different stack if you justify it, but default to the above.

## Domain model (design these tables/entities)

- **User** — id, email (unique), password hash, name, email-verified, created/updated, status.
- **Organization** (a.k.a. account/tenant) — id, name, slug, plan, billing info ref, created.
- **Membership** — user ↔ organization with **role** (`owner` | `admin` | `member`); invites.
- **App** (monitored application / "project") — id, org id, name, environment
  (`production`/`staging`/…), created, last-seen-at (updated by the collector), status.
- **ApiKey** — id, app id, label, **key hash**, display prefix, created-at, last-used-at,
  revoked-at. Belongs to an app; an app may have several (for rotation).
- **Plan** — code (`free`/`pro`/`team`/`enterprise`), limits (monthly event quota, retention
  days, max apps, max members), price.
- **Subscription** — org ↔ plan, status, current period start/end, payment-provider ids.
- **UsageRecord** — org/app, period, events ingested (fed by the collector), for quota + billing.
- **Invite**, **PasswordReset**, **EmailVerification** tokens as needed.

## Endpoints to design & implement (minimum)

Group under `/api/v1`. For **each** endpoint specify: method, path, auth requirement, request
body/query, success response shape + status, and error responses (400/401/403/404/409/422/429).

**Auth & account**
- `POST /auth/register` — create user (+ first org), send verification.
- `POST /auth/login` — issue access/refresh tokens (or session).
- `POST /auth/refresh`, `POST /auth/logout`.
- `POST /auth/verify-email`, `POST /auth/forgot-password`, `POST /auth/reset-password`.
- `GET  /me` — current user + orgs/memberships.
- `PATCH /me`, `POST /me/change-password`, `DELETE /me` (danger zone).

**Organizations & team**
- `GET /orgs`, `POST /orgs`, `GET /orgs/{orgId}`, `PATCH /orgs/{orgId}`, `DELETE /orgs/{orgId}`.
- `GET /orgs/{orgId}/members`, `DELETE /orgs/{orgId}/members/{userId}`,
  `PATCH /orgs/{orgId}/members/{userId}` (role change).
- `POST /orgs/{orgId}/invites`, `GET /orgs/{orgId}/invites`,
  `POST /invites/{token}/accept`, `DELETE /orgs/{orgId}/invites/{inviteId}`.

**Apps**
- `GET /orgs/{orgId}/apps`, `POST /orgs/{orgId}/apps`,
  `GET /orgs/{orgId}/apps/{appId}`, `PATCH …`, `DELETE …`.

**API keys**
- `GET /apps/{appId}/keys` — list (masked, with prefix + last-used).
- `POST /apps/{appId}/keys` — create; **return the full secret once** in the response only.
- `POST /apps/{appId}/keys/{keyId}/rotate`, `DELETE /apps/{appId}/keys/{keyId}` (revoke).

**Plans, billing & usage**
- `GET /plans` — public plan catalog (for the pricing page).
- `GET /orgs/{orgId}/subscription`, `POST /orgs/{orgId}/subscription` (subscribe/change plan),
  `DELETE /orgs/{orgId}/subscription` (cancel).
- `GET /orgs/{orgId}/usage` — events ingested vs quota for current/selected period.
- `GET /orgs/{orgId}/invoices`, billing-provider webhook `POST /webhooks/billing`
  (signature-verified).

**Public / marketing-support**
- `GET /plans` (above) and `POST /contact` (contact-form / sales lead capture), rate-limited.
- `GET /health` and `GET /ready` for the service itself.

## Integration with the collector (important)

The collector authenticates inbound telemetry by `X-Api-Key`. Define the contract between the two
services and implement this service's side:
- **Internal key-validation endpoint** for the collector, e.g.
  `POST /internal/keys/validate { key }` → `{ valid, appId, orgId, planLimits, withinQuota }`,
  protected by a service-to-service secret (not a user JWT). Cacheable; document TTL.
- **Usage ingest** from the collector, e.g. `POST /internal/usage { appId, eventCount, periodTs }`
  to increment `UsageRecord` and update `App.last-seen-at`. Also service-authenticated.
- Decide and document whether key validation is **pull** (collector calls this API) or **push**
  (this API publishes key changes); pick one, justify, keep it simple.

## Cross-cutting requirements

- **AuthZ**: enforce org membership + role on every org-scoped route (owner/admin can manage
  members/billing/keys; member is read-mostly). Never let a user touch another org's data.
- **API-key secrets**: never store or log the plaintext; hash + prefix only; constant-time
  compare on validation.
- **Rate limiting** on auth, contact, and validation endpoints; **quota enforcement** surfaced as
  `429` with a clear body when an org exceeds its plan's event quota.
- **Validation** on all inputs; consistent error envelope (e.g. `{ error: { code, message,
  details } }`); correct status codes.
- **Pagination** (cursor or page/limit) + filtering/sorting on all list endpoints.
- Migrations (EF Core), seed data for plans, and a `docker-compose` (API + Postgres) for local run.
- **Tests**: integration tests for the auth flow, key create/rotate/revoke, role enforcement,
  and quota `429`.
- **OpenAPI** doc exposed; README with run instructions.

## Deliverables

1. A written **API design doc**: the full endpoint table (method/path/auth/req/res/status), the
   ER/data model, the auth model, and the collector-integration contract — **before** large-scale
   coding. Confirm the design, then implement.
2. The implemented scaffold: project structure, entities + migrations, auth, the endpoints above
   (at least auth + apps + API keys + plans/usage end-to-end), validation, error handling.
3. `docker-compose` + README + seeded plans + OpenAPI.
4. Tests covering the critical flows listed above.
5. A list of assumptions and any deviations from this prompt, with reasons.

## Constraints

- Keep this service focused on the **control plane**. Do **not** build telemetry ingestion or
  querying here — that's the collector. The only telemetry-adjacent surface here is the
  `/internal/*` key-validation + usage contract.
- Don't hardcode secrets; read from configuration/env. Note in the README that the existing repo
  has had cleartext-credential issues — do not repeat that pattern.

Begin by producing the API design doc + data model and the endpoint table, then scaffold and
implement.
