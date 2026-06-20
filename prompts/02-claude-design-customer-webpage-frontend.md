# Prompt — Design the Customer-Facing Website (front end)

> **Paste this whole file into a fresh Claude (design) session.** It is self-contained.
> Audience for the work: prospective and existing **customers** of the Metriquee SaaS.

---

## Your role

Act as a senior brand + product designer and front-end engineer. Produce a **complete,
high-fidelity design** for the **public-facing Metriquee website and customer account portal** —
the marketing site that converts visitors into sign-ups, plus the authenticated portal where
customers manage their account, apps, API keys, and billing. Deliver a design system **and**
interactive, high-fidelity mockups (HTML/CSS prototype preferred so flows can be clicked
through). This is distinct from the in-product telemetry dashboard — it is the **storefront +
account control plane**, not the observability console.

## Product context (what you're selling)

Metriquee is drop-in **application monitoring for .NET / ASP.NET Core apps**. A developer adds
one NuGet package (`Metriquee.NetCore`) and two lines of code:

```csharp
builder.Services.AddLogCollector(opts => {
    opts.Sender.EnableSenderSink = true;
    opts.Sender.ApiKey  = "your-api-key";
    opts.Sender.BaseUrl = "https://collect.metriquee.com";
});
app.UseLogCollector();
```

…and the app automatically starts reporting **HTTP requests, unhandled exceptions, runtime
metrics (CPU/memory/GC/thread-pool), and health status** to Metriquee. No agents to install, no
config sprawl. The value props to sell: **5-minute setup**, **zero-config defaults**,
**automatic exception + request tracing correlated by trace ID**, **runtime/GC/thread-pool
insight specific to .NET**, **secret masking built in** (sensitive headers and JSON fields are
masked before they ever leave the app), and a **self-host option** for teams that need data to
stay in their own infra.

Primary persona: **.NET backend developers and small engineering teams** who want Sentry/Datadog-
style insight without heavy setup. Tone: technical, credible, fast, friendly — developer-first,
not enterprise-stuffy.

## Marketing site — pages to design

1. **Landing / home** — hero with the one-liner + the 2-line code snippet above as the
   centerpiece ("monitor your .NET app in 5 minutes"), primary CTA (Start free), social proof
   strip, a
   product screenshot/peek of the dashboard, and 3–4 feature highlight blocks (Requests &
   tracing, Exceptions, .NET runtime metrics, Health & uptime). Secondary CTA: View docs.
2. **Features** — deeper dive on each pillar with annotated UI imagery: HTTP request logging &
   `traceId` correlation, exception grouping, runtime metrics (CPU/GC/thread pool/RPS), health
   monitoring, built-in secret masking, batching/low overhead.
3. **Pricing** — clear tiered plans (e.g. Free / Pro / Team / Self-Hosted-Enterprise) with a
   feature comparison table. Make ingest-volume / retention the natural axis of differentiation.
   Include a **"Self-hosted"** column (own your data, runs in your infra).
4. **Docs / quick-start landing** — a getting-started page styled to match (install, configure,
   first data in 5 min), with the code snippets. Link out to full docs.
5. **Self-hosting page** — for teams that want to run Metriquee themselves: what you get, system
   requirements, "deploy with Docker", links to install guide. Positions self-host vs cloud.
6. **Secondary pages**: About, Contact/Support, Changelog/Blog index, legal (Privacy/Terms),
   404. Keep these lighter but on-brand.

## Account portal (authenticated) — screens to design

1. **Sign up / log in / forgot password** — fast, developer-friendly (email + password and a
   social/SSO option). Sign-up should land the user on a "create your first app → copy your API
   key → paste this snippet" onboarding.
2. **Onboarding / first-run** — create first app, reveal API key once, show the exact code
   snippet to paste, and a "waiting for first event…" live state that flips to success when data
   arrives. This is the most important conversion-to-activation moment — design it carefully.
3. **Apps/projects** — list of the customer's monitored apps, each with name, environment,
   API key (masked, copy, rotate, revoke), last-seen, and a link **into the telemetry dashboard**
   (the dashboard itself is a separate product surface — just link to it).
4. **API keys** management — create/rotate/revoke, per-key scope/label, last-used.
5. **Team / members** — invite teammates, roles (owner/admin/member), pending invites.
6. **Billing** — current plan, usage this period (events ingested / retention), upgrade/downgrade,
   payment method, invoices.
7. **Account settings** — profile, password, notification preferences, danger zone (delete).

## UX & content requirements

- The **2-line install snippet** is the hero asset — feature it prominently with a copy button
  and syntax highlighting; show the API key placeholder clearly.
- Onboarding must make "**time to first event**" feel instant and rewarding (live success state).
- Pricing must make the **self-hosted vs cloud** choice obvious and unintimidating.
- Trust signals: secret-masking / "your secrets never leave your app", open self-host option,
  low overhead, MIT-licensed agent.
- Every marketing page needs a clear single primary CTA; reduce decision fatigue.
- Realistic empty/loading/error states for portal screens (no apps yet, key copied, payment
  failed, invite pending).

## Design system to define

- **Brand**: pick a coherent palette, logo treatment direction, and personality for "Metriquee"
  (modern developer-tool brand — confident, clean, a little technical). Define primary/accent/
  neutral tokens for **light and dark** themes (marketing typically light, portal supports both).
- Typography scale (a characterful display/heading face + readable body + monospace for code).
- Components: buttons, nav/footer, hero, feature card, pricing card + comparison table, code
  block with copy, testimonial, stat strip, form inputs, auth card, app/key list rows, billing
  cards, badges, toasts.
- Spacing/radius/elevation tokens; motion guidelines for tasteful, performant micro-interactions.
- **Responsive: mobile-first** for the marketing site (most landing traffic is mobile); the
  portal is responsive but desktop-leaning.
- Accessibility: WCAG AA, keyboard-navigable forms/nav, visible focus, reduced-motion support.

## Deliverables

1. A short **brand + design rationale** (positioning, personality, palette, type choices).
2. The **design system / tokens** and core component specs.
3. **High-fidelity, interactive mockups** (clickable HTML/CSS prototype preferred) of: the
   landing page, pricing, the quick-start/docs landing, and the portal onboarding + apps/API-keys
   + billing screens at minimum. Show responsive (mobile + desktop) for the landing page.
4. Final **marketing copy** for the pages you design (headlines, subheads, feature blurbs, CTA
   labels) — write it, don't leave lorem ipsum.
5. A list of **assumptions** about the backend (auth flow, plan/usage data, key lifecycle) so the
   web API can be built to match.

## Constraints

- Keep claims grounded in what the agent actually does (the four telemetry types above; .NET /
  ASP.NET Core focus; batching; masking; self-host). Don't promise capabilities the product
  doesn't have (e.g. no APM tracing spans/flame graphs, no front-end RUM, no log search beyond
  the captured events).
- Marketing site should feel fast and lightweight; prototype in plain HTML/CSS and note the real
  stack you'd recommend (e.g. Next.js/Astro) and component library if any.

Start with positioning + brand direction, then the design system, then build the landing page,
then pricing/docs, then the portal onboarding flow.
