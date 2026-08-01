# Architecture

Everdue is one process serving the API and the web app on one port, one database, and a handful of
background services. This document is the map, and the reasoning behind the decisions that would
otherwise look arbitrary.

---

## Layout

Clean Architecture as **folder boundaries inside one project**, not a project per layer:

```
src/Server/
  Domain/          entities, enums, recurrence math, the transition matrix — references nothing
  Application/     CQRS commands/queries + handlers (Common.Mediator), DTOs, abstractions
  Infrastructure/  EF Core (dual provider), Identity, tenancy, channels, files, webhooks, migrations
  Engine/          background services: occurrences, digest, reminders, both outboxes, Telegram polling
  Api/             minimal-API endpoint modules, one per resource, plus the gates and ProblemDetails
  Hosting/         content root and pipeline composition
src/Server.Tests/  xUnit — recurrence battery, engine integration, API integration, report correctness
src/client/        React + TypeScript + Vite + Mantine
deploy/            Dockerfile, compose, systemd unit, Windows service script, publish script
docs/              this documentation, and the committed OpenAPI contract
```

Rules enforced in review:

- `Domain` references **nothing** — no EF, no ASP.NET, no DI.
- `Application` reaches persistence **only** through `IEverdueDbContext`, and never touches
  `Infrastructure`.
- Endpoints **bind and dispatch**; they never decide. Rules live in handlers.
- DTOs are never EF entities.
- One responsibility per file; a handler past ~120 lines has something in it that belongs elsewhere.

## The request path

```
HTTP → security headers → authentication (cookie | X-Api-Key) → API-key gate → password-change gate
     → endpoint (bind) → mediator → validation behaviour → handler
     → IEverdueDbContext (global tenant filter) → DTO → JSON
```

Two authentication schemes, deliberately not interchangeable: a **cookie** for people, an
**`X-Api-Key` header** for scripts. The API-key gate then refuses any endpoint not explicitly opted in,
and refuses writes for a read-only key — reachability is an allow-list, not a role.

Errors are RFC 7807 `ProblemDetails` with a stable `code`. **`409` means "a rule refused this"**, and
the `detail` names the rule.

## The background services

| Service | Cadence | Does |
|---|---|---|
| Occurrence engine | every `Engine:TickMinutes`, and once at startup | spawns occurrences, records misses |
| Digest | checks every 10 min | the manager digest at the tenant's local hour |
| Due-today reminders | checks every 10 min | at the tenant's local reminder hour |
| Notification dispatcher | every 30 s | drains the delivery outbox with backoff |
| Webhook dispatcher | every 15 s | drains the webhook outbox, auto-disables dead subscriptions |
| Telegram polling | long poll | the account-link flow — **no webhook**, so NAT is fine |

None of them keeps a "last run" marker. The engine re-derives the ledger, the digest remembers the
last local date it sent to each subscriber, and the reminder relies on a unique key on the
notification. Restart-safety therefore needs no extra state.

---

## Key decisions

**IDs are GUID v7.** Index-friendly, opaque in URLs, merge-safe for a future hosted version.

**`TenantId` everywhere, one global query filter.** A single configured tenant today; the schema is
what makes the hosted version a no-migration step later. Exactly two tables sit outside the filter,
each documented on its class: channel settings (a system-scope row must be readable while serving a
tenant) and API keys (authentication happens before the tenant is known — the key is what resolves it).

**Timestamps are UTC.** All period maths converts through the tenant's IANA zone as a *civil date*
first, so daylight saving never moves a period boundary off local midnight.

**The engine is stateless.** Every tick re-derives the ledger from data, which makes catch-up after
two weeks of downtime and idempotency under a double tick the *same code path* rather than two
features. Inserts are guarded by a unique index on `(ResponsibilityId, PeriodStart)`.

**Transitions are explicit endpoints**, never a status `PATCH`. The matrix lives in one file
(`Domain/StatusTransitions.cs`) and anything not in it is refused.

**Lateness comes from the period, not the status.** The engine ticks on a timer; an occurrence
finished just after its period ended is still `Open` when the user clicks, and recording that as
on-time would erase a miss on a timer boundary. The insight reports extend the same rule to the
compliance denominator: a period whose end has passed is judged now, not at the next tick.

**Insights are computed on read, never precomputed.** Three years of a busy 30-person tenant is around
50,000 occurrence rows; SQL narrows them and the bucketing happens in C#, because a tenant-local ISO
week is not portably derivable from a UTC timestamp on both providers. If a p95 insight response ever
passes a second on real data, the answer is a nightly summary table — **not** a lookback window, which
would change what the numbers mean.

**Hold history is reconstructed from `WorkItemEvents`**, pairing each entry into `OnHold` with the next
exit from it. The reason lives in the entry event's payload because the column on the item is cleared
the moment the hold is released — which is why hold aging can answer for history nobody was recording
for reports.

**Two providers from one model.** SQLite by default (the whole state is one file) and PostgreSQL by
configuration. That means LINQ only — no raw SQL, no provider-specific functions, no JSON predicates
pushed to the database — and a migration per provider, always both. CI runs the whole suite against
each.

**CSRF posture**: same-origin SPA + `SameSite=Strict` HttpOnly cookie + no CORS. No token machinery to
get wrong.

---

## Security posture

- Cookies are encrypted with data-protection keys kept in `DataDir/keys`, on the same volume as the
  database. Left to its default the framework would look for a user profile, find none in a container,
  and fall back to an in-memory key ring — logging everyone out on every restart.
- Response headers on every request: `nosniff`, `Referrer-Policy: no-referrer`,
  `X-Frame-Options: DENY`, a `Permissions-Policy`, and a CSP with `script-src 'self'` (no inline
  script) outside Development. HSTS only when `Security:RequireHttps` is on.
- Passwords: 10 characters minimum with mixed case and a digit; lockout after 10 failures for 15
  minutes; sign-in additionally rate-limited per address, because lockout cannot see one password
  being sprayed across many accounts.
- Sign-in answers identically for a wrong password, an unknown account and a deactivated one — the API
  never confirms whether an address has an account.
- Revoking access (deactivating a user, or demoting an administrator) rotates their security stamp,
  and cookies revalidate every 2 minutes, so an open browser loses access promptly.
- Attachment bytes never enter the database and never take the uploaded filename to disk: the storage
  key is `{tenantId}/{attachmentId}`, so path traversal is impossible by construction rather than by
  sanitising a string correctly.

## Performance

- Query counts are **flat, not per-row**: a 100-item board is 3 queries whether the table holds 36 rows
  or 800, and the heaviest report is a constant 8. Tests count SQL at both sizes.
- Responses are Brotli/gzip compressed (the SPA bundle drops ~62%), content-hashed assets are served
  `immutable` for a year, and the shell is `no-cache` so a deploy is picked up immediately.

## The client

React 19 + TypeScript + Vite + Mantine 9, same origin as the API — in development the dev server
proxies `/api`, so the `SameSite=Strict` cookie behaves identically in both. Server state is React
Query; there is no store. API types are **generated** from the committed contract and never
hand-written. Navigation is data (`lib/navigation.ts`), which is what lets the navbar and every page
header agree on a screen's icon and colour. Every user-visible string goes through i18n, and CI fails
on a missing key in either language.

The in-app user manual lives at `src/client/src/features/help/content/{en,es}/` as markdown and is
rendered by a small dependency-free renderer — no HTML injection, so the CSP posture is untouched.
