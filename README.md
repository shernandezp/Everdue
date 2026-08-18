# Everdue

**Operational accountability for small and medium businesses.** A responsibility is *ever due* — it
never finishes, it comes due again every period, and whether it happened is on the record.

Everdue is not a project manager and not a task manager. It answers exactly one question:
**what work needs to happen, what happened, and what requires attention?**

Two things make it different from every tool we could find:

1. **A missed-by-default occurrence ledger.** When a period passes uncompleted, the occurrence is
   recorded as *missed* — permanently — and the next one spawns anyway. Mainstream tools either
   stall the series (Asana, MS Planner) or roll the date forward and erase the miss (ClickUp,
   Trello). Because their databases never contain the miss, they cannot produce the reports below.
2. **An entity dimension with health reports.** Work optionally belongs to a customer, supplier,
   piece of equipment, department or company, and five fixed reports are computed from the ledger —
   no report-oriented data entry anywhere.

Because the ledger keeps every period forever, the same rows answer the questions a manager asks
months later: which responsibilities are chronically missed, who is blocked rather than unreliable,
which clients the team's work goes to, and where waiting time goes. That is what the
[Insights screens](#insights--operational-memory) answer, and they needed no new data entry to build.

---

## Live demo

**<https://everdue.sergiohernandezp.com>** — a hosted instance pre-loaded with six months of
realistic (fake) history, so every report and insight screen has something to say.

| Sign in as | Email | Password |
| --- | --- | --- |
| Administrator | `ana@demo.everdue.app` | `EverdueDemo2026!` |
| Member | `john@demo.everdue.app` | `EverdueDemo2026!` |

The credentials are public on purpose, and none of the data on it is real. The instance is wiped and
reseeded every Monday, so anything you change there is temporary by design. To run the same tour on
your own machine instead, see [Demo mode](#demo-mode-one-command) below.

---

## Install

Two paths need no toolchain at all — the published Docker image and the release archives. Building
from source works too and is documented below.

### Docker (one command)

```bash
docker run -d --name everdue -p 8080:8080 -v everdue-data:/data \
  -e Tenant__TimeZoneId=America/Bogota \
  ghcr.io/shernandezp/everdue:latest
```

Open <http://localhost:8080>. With no bootstrap credentials configured, the first start creates an
admin **`admin@everdue.local`** with a random password printed **once** in the log
(`docker logs everdue`) — sign in and change it when prompted. To choose the first account yourself,
add `-e Bootstrap__AdminEmail=… -e Bootstrap__AdminPassword=…`. Images are tagged per release
(`ghcr.io/shernandezp/everdue:<version>`) as well as `:latest`.

### Download a release (no toolchain)

Grab the archive for your platform from the GitHub **Releases** page —
`everdue-<version>-win-x64.zip`, `…-linux-x64.tar.gz`, `…-linux-arm64.tar.gz` or
`…-osx-arm64.tar.gz` — unpack it, and run `./Everdue.Server` from the unpacked folder.

One self-contained executable plus `appsettings.json` and a `wwwroot/` folder. No .NET runtime to
install; the database is one SQLite file under `data/`. Untrimmed on purpose — trimming breaks EF
Core, and ~90 MB is the price of "copy one folder and run it".

It listens on **<http://localhost:5000>** unless told otherwise. To bind another address or port —
here, reachable from the rest of the LAN on 8080 — set `ASPNETCORE_URLS` (or its `EVERDUE_`-prefixed
equivalent, `EVERDUE_URLS`) before starting:

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 ./Everdue.Server
```

The same first-run behaviour applies: configure `Bootstrap:AdminEmail`/`AdminPassword` in
`appsettings.json` to choose the first account, or take the generated one from the startup log.

### Demo mode (one command)

Before installing anything for real, look at it with data in it — on the [live demo](#live-demo)
above, or locally:

```bash
git clone https://github.com/shernandezp/everdue && cd everdue
docker compose -f deploy/docker-compose.demo.yml up --build
```

Open <http://localhost:8080> and sign in as **`ana@demo.everdue.app` / `EverdueDemo2026!`**.

You get six months of believable history: on-time completions, late ones, real misses, one chronically-missed
responsibility, hold intervals across several reasons, checklists, a photo-proof rule, and a mix of one-off tasks.
Every report and every insight screen has something on it — which an empty install cannot show you, because the
ledger, the compliance strip and the health table are all invisible without history.

It is a **demo**: the passwords are public and it lives in its own volume. The startup seeder refuses outright on a
database that already contains data, so it cannot damage a real install.

**Already installed and want the same tour?** On an install with `Demo:AllowReset=true` (**off by
default** — the demo compose file sets it), an administrator can switch demo mode on from **Settings** in the
running app, and switch it off again when they are done. Read the warning first: **it deletes everything in the
workspace, in both directions** — every work item, responsibility, entity, attachment and user account except the
one performing it, with no undo. It asks for the workspace name typed out and your own password before it will
run. See [docs/configuration.md](docs/configuration.md#demo-mode).

### Building from source

The compose file builds the image locally instead of pulling it:

```bash
git clone https://github.com/shernandezp/everdue && cd everdue
cp deploy/.env.example deploy/.env        # edit EVERDUE_ADMIN_EMAIL / EVERDUE_ADMIN_PASSWORD
docker compose -f deploy/docker-compose.yml up -d --build
```

Open <http://localhost:8080>, sign in with the bootstrap admin, and choose a new password when asked.

Or produce the same folder a release archive contains (needs the .NET SDK and Node):

```powershell
./deploy/publish.ps1 -Runtime win-x64        # or linux-x64, linux-arm64, osx-arm64
cd publish/win-x64
./Everdue.Server                             # http://localhost:5000
```

### Windows service

```powershell
# elevated prompt
./deploy/install-windows-service.ps1 -Path 'C:\Everdue\Everdue.Server.exe'
```

### Linux daemon

```bash
sudo cp -r publish/linux-x64/* /opt/everdue/
sudo cp deploy/everdue.service /etc/systemd/system/
sudo systemctl enable --now everdue
```

---

## Configuration

Everything is `appsettings.json`, overridable by environment variables (`Section__Key`) and
command-line arguments. **The full reference is [docs/configuration.md](docs/configuration.md)**; these
are the settings almost every install touches:

| Setting | Default | Notes |
|---|---|---|
| `Tenant:TimeZoneId` | `UTC` | **IANA** id. Every period and due date is computed in this zone — set it before anyone creates responsibilities. |
| `Bootstrap:AdminEmail` / `AdminPassword` | *(empty)* | First-run admin. Unset ⇒ `admin@everdue.local` is generated with a random password printed once in the startup log. Either way a password change is forced at first login. |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres`. |
| `ConnectionStrings:Default` | *(empty)* | Empty + SQLite ⇒ `DataDir/everdue.db`, WAL enabled. Required for Postgres. |
| `DataDir` | `./data` | SQLite file, data-protection keys **and attachment files**. This directory is the whole backup. |
| `Tenant:DefaultLanguage` | `es` | `es` or `en`; a user preference overrides it. |
| `Security:RequireHttps` | `false` | Marks the auth cookie `Secure` and sends HSTS. Turn it on when TLS terminates at Everdue; leave it off behind a plain-HTTP reverse proxy, or the browser will drop the cookie and logins fail silently. |
| `App:PublicBaseUrl` | *(empty)* | Where this install is reachable, used to put a link in a notification. Empty means messages carry no link, which beats carrying a broken one. |

First run applies migrations, seeds the default tenant and creates the bootstrap admin. All of it is
idempotent and repeats safely on every start. If `Bootstrap:AdminEmail`/`AdminPassword` are not set
on a fresh database, the app **generates** an admin — `admin@everdue.local`, random password, printed
once in the startup log in a banner you cannot miss — so a zero-config first run still yields an app
somebody can sign into. The forced first-login password change applies to it like any bootstrap admin.

The app finds `appsettings.json` and the built SPA next to its own executable, not in the working
directory, so it behaves identically started by hand, by `sc.exe`, or by systemd.

Installing, upgrading, backing up and diagnosing an install: **[docs/operations.md](docs/operations.md)**.

---

## The core — work, the ledger and the reports

- Users and roles (Admin / Member), cookie authentication, no self-service registration.
  Members run the board and the entity screens; administrators additionally get the dashboard, the
  reports and the admin screens. Members can read the user directory (they have to, to assign work)
  but see only active colleagues, and cannot create, edit or reset anyone.
- **Anyone may work and edit anyone's item**, including handing it to someone else — in a team this
  size that is cover, not overreach. Every edit writes an `Updated` event naming who changed which
  field and what the old value was, which is what makes the openness safe (and is what the
  reassignment history is built from). The two exceptions are undoing a completion and
  cancelling a task: both erase a record, so they stay owner-or-admin.
- Entities (customer / supplier / equipment / department / company) — **name, type, active, nothing
  else**. Departments as a separate lookup for *who executes* the work.
- One-off tasks and recurring responsibilities (daily / weekly-on-days / monthly-on-day / yearly).
- The occurrence engine: spawns on schedule regardless of the previous period, records misses, and
  catches up from any amount of downtime.
- Status workflow: `Open · InProgress · Completed · CompletedLate · Missed · OnHold · Cancelled`,
  with a mandatory hold reason from a five-item taxonomy. Overdue is derived, never stored.
  *In progress* is a coordination signal only: it changes no report, it never stops a period being
  missed, and a missed item can never move back into it.
- Board (**To do · In progress · On hold · Missed · Done**) with an owner picker for the manager
  view, list view, and an item drawer with full event history and comments.
- Five reports: exception dashboard, entity health, neglect, blocked-by-entity, entity timeline —
  every number drilling through to the work-item list that produces it.
- Daily digest e-mail, localized per recipient.
- Spanish and English throughout, per-user preference over tenant default.

---

## Keeping people in the loop — notifications and channels

**Nothing here is load-bearing.** With no channel configured, notifications are still recorded and
read in the app through the in-app bell, no request path changes, and no error is shown. An
unconfigured channel is a channel that is *not offered*, not a broken one.

- **Notifications**: assigned to you · due today · went missed · mentioned in a comment · your item
  put on hold by somebody else. In-app bell with an unread count, polled once a minute.
- **Channels** — e-mail, **Telegram** and **WhatsApp** behind one interface, configured per tenant
  with system-scope fallback. Credentials are encrypted with the key ring in `DataDir`. Delivery is
  an outbox with retry and backoff: one dead channel never delays another, another person, or any
  request.
  - **Telegram** is the recommended channel for staff who do not sit at a desk: free, no approval
    process, and it uses **long polling**, so an install behind a home or office router works.
  - **WhatsApp** needs a WhatsApp Business Account you already run, plus a **utility template
    approved by Meta per notification type** (template names are configuration, so an approval can
    land without a deploy). Business-initiated messages are billed per message. There is no webhook,
    so **"sent" means Meta accepted it**, not that it reached a phone — the health screen says so.
- **Manager digest** by subscription: daily or weekly, optionally scoped to one department, now with
  on-hold aging and neglect sections. An administrator with no subscription row is still a daily
  subscriber, so an upgrade changes nothing without being asked to.
- **Sign in with Google**, alongside password login. Existing, active users only — no
  auto-provisioning, and Google's `email_verified` claim is required.
- **Reassignment**: hand over a responsibility (future occurrences follow automatically), or hand
  over everything a departing person owns in one call. Owner changes are recorded as `Reassigned`
  events and counted on the dashboard.
- **Attachments** on work items — local disk behind a storage interface, allow-listed types, and an
  authenticated download endpoint. No virus scanning: that is a hosted-version concern.
- **Quality of life**: status and hold-reason filters, personal saved views, and bulk complete /
  reassign / reschedule with per-item results.

Deliberately **out**: time tracking, automations, native mobile, a report builder, SMS, and web push.
*(Checklists and custom fields exist, bounded — see below.)*

---

## Insights — operational memory

The intelligence layer. **No new data entry and no new tables**: every number below is computed on
read from work items and their event history, so it works retroactively on whatever history an
install already has. Four screens under **Insights**, administrators only.

- **Compliance per responsibility** — completed on time out of every period that has *ended*, with
  the trend behind it and a page per responsibility showing the ✅/❌/⏸ strip of its individual
  periods. A late completion counts as a miss, exactly as it does everywhere else in Everdue.
  A period that has ended counts as a miss **immediately**, whether or not the engine's next tick has
  flipped the row yet: a compliance number must not depend on when a background service last ran.
- **Chronically delayed** — responsibilities that missed *K of their last N periods* (default 3 of 8,
  configurable). It appears as a block on the exception dashboard as well as on its own.
- **Reliability per person** — the same formula per owner, over recurring work only, because a
  one-off task can never be missed and counting it would inflate every rate with work that cannot
  fail. One-off completions are shown as their own column.
- **Completed work by entity** — how much finished work each customer, supplier or machine accounts
  for, month by month. It is a **count of work items, not effort in hours**: Everdue holds no time
  data, so a two-minute call and a full-day inspection both count as one. Work nobody linked to an
  entity is reported as a total rather than quietly left out.
- **Hold aging** — how long work waits per reason and per entity, rebuilt from the event log. The
  figures are **calendar days**: nights and weekends are inside them. Business-hours arithmetic would
  need a shift and holiday calendar, which Everdue does not have and is not going to grow.
- **The first two charts**, and only two: a monthly bar chart of completed work and a compliance line
  per responsibility, plus a sparkline in each compliance row. Tables remain the default everywhere.

### Reliability is management information, not a scoreboard

Everdue has **no leaderboard, no ranking, no badges and no per-person targets**, and will not grow
any. The reliability screen exists so a manager can decide *where to help*, and it is built to be
read that way:

- Insights are **administrator-only**. Nobody sees a colleague's numbers, and there is no per-person
  notification about their own.
- A percentage never appears without the volume it rests on, and a rate over fewer than five
  concluded periods is **not shown at all** — 95% of 200 is not 100% of 3.
- **External waits count in the rate and are shown beside it.** Taking them out would make the
  denominator something a person could manage by parking work on hold, which is the opposite of what
  the hold taxonomy is for. What "waiting on the customer" cost is visible in the same row as the
  miss it explains.
- Numbers are attributed to an item's **current owner**, and the screen says so, with a count of
  hand-overs in the window beside it.

### One thing to know about back-dated responsibilities

Creating a responsibility with a **start date in the past** makes the engine generate real `Missed`
occurrences for every period between then and now — periods nobody was ever asked to do. Those rows
are indistinguishable from genuine misses in a compliance rate, because a miss in the ledger is a
miss; that is the guarantee the whole product rests on. Set the start date to **today** unless you
deliberately want the history.

### Upgrading, and backing up

Stop the app, **copy the data directory**, start the new binary. Migrations apply on startup, and
nothing needs configuring afterwards.

The whole state is that **data directory**, not just the database file:

```
data/
  everdue.db      the database (plus -wal / -shm while running)
  keys/           data-protection keys — auth cookies and channel credentials
  files/          attachment bytes
```

Losing `keys/` signs everyone out, means channel credentials have to be re-entered *and* means webhook
signing secrets have to be regenerated; losing `files/` leaves attachment records pointing at bytes
that are gone. Full procedure, including the SQLite caveat and what to check when something looks
wrong: **[docs/operations.md](docs/operations.md)**.

---

## Fitting more businesses

Everything here is about *work*, or about getting work into the system.

- **Checklists.** A responsibility carries an ordered template; every occurrence gets a **snapshot** of it at spawn,
  so editing the template never rewrites history. Any item can also take ad-hoc steps. Optionally, an occurrence
  **cannot be completed until its required steps are ticked** — enforced by the server, not the screen. Progress
  shows on the card, the row, the drawer and the entity timeline, and **nowhere else**: there is no checklist report
  and no checklist metric, because that is the line between an accountability tool and an audit tool.
- **Proof of completion.** Optionally, an occurrence cannot be completed without a photo or file. The attachments
  panel has a camera button, so on a phone it is two taps.
- **Custom fields on entities** — an account manager on a customer, a serial number on a machine. Capped at ten per
  entity type, four scalar types, one JSON column, **display-only**: nothing filters, sorts, reports or sends a
  webhook on them. They exist because the "account manager" column is why a team still has the spreadsheet.
- **CSV export** of the work list, the three tabular reports, the four insight tables, and a raw per-table dump.
  Each export dispatches the same query its screen does, so the file matches the screen by construction. UTF-8 with
  a BOM (Excel reads accents), OWASP formula-injection guarded, and **no silent truncation** — over the row limit is
  a refusal with an instruction, never a shorter file.
- **CSV import** of entities and one-off tasks: upload, confirm the suggested column mapping against real parsed
  rows, commit. Row-level results, a downloadable error list, duplicates **skipped and reported** — an import creates
  or skips, never overwrites. It reads Spanish Excel's semicolons. Linked from the empty states, because that is
  where somebody with a spreadsheet actually is.
- **A public API.** The contract is committed at [docs/openapi/v1.json](docs/openapi/v1.json) and CI fails when it
  and the code disagree; the compatibility policy is written down in [docs/api.md](docs/api.md). **API keys** are
  hashed, scoped read/write, rate-limited per key, and confined to an **endpoint allow-list** — a leaked key cannot
  create a user or read a channel credential even if its actor is an administrator.
- **Webhooks** over six events, signed to the [Standard Webhooks](https://www.standardwebhooks.com) spec so an
  off-the-shelf library verifies them. Outbound only, at-least-once with a stable `webhook-id` for deduplication,
  exponential backoff, and auto-disable after ten consecutive failures with a banner saying so.
- **Additional languages as data.** A translation is one JSON file, three `.resx` files and two list entries — no
  `.tsx` change anywhere. The server owns the supported-language list and the client renders from it. See
  [docs/translating.md](docs/translating.md). *(Everdue itself ships only Spanish and English: a machine-translated
  locale nobody on the team can review reads as an abandoned product.)*
- **[Live demo](#live-demo)** and **Demo mode**, above — the fastest way to see all of this with
  data in it.

### Two boundaries worth stating plainly

**Checklists stop at execution.** No scoring, no pass/fail grading, no per-item photo requirements, no reusable
template library, no inspection reports. Those make an audit tool, and that market belongs to somebody who has been
building one for a decade.

**Custom fields never *do* anything.** No formulas, no relations between entities, no required-field workflows, no
filtering. The moment a custom field drives behaviour, entities have stopped being thin references and the drift
into an ERP has begun. That boundary is the product, not a limitation of the implementation.

---

## Architecture

The short version. The full one, including the security and performance posture, is
**[docs/architecture.md](docs/architecture.md)**.

Clean Architecture as **folder boundaries inside one project**, not a project per layer:

```
src/Server/
  Domain/          entities, enums, recurrence math, the transition matrix — references nothing
  Application/     CQRS commands/queries + handlers (Common.Mediator), DTOs, abstractions
  Infrastructure/  EF Core (dual provider), Identity, tenancy, SMTP, migrations per provider
  Engine/          OccurrenceEngine + DigestService (both BackgroundService)
  Api/             minimal-API endpoint modules, one per resource, plus ProblemDetails
                   Domain/Insights + Application/Insights hold the insight metrics: pure bucket and rate
                   maths in Domain, one reader and one handler per report in Application
src/Server.Tests/  xUnit — recurrence battery, engine integration, API integration, report correctness
src/client/        React + TypeScript + Vite + Mantine
deploy/            Dockerfile, compose, systemd unit, Windows service script, publish script
```

Rules enforced in review: `Domain` references nothing; `Application` reaches persistence only
through `IEverdueDbContext`; endpoints bind and dispatch, never decide; DTOs are never EF entities.

**Key decisions**

- **IDs are GUID v7** — index-friendly, opaque in URLs, merge-safe for a future hosted version.
- **`TenantId` everywhere, one global query filter.** Single configured tenant today; the schema is
  what makes the hosted version a no-migration step later.
- **Timestamps are UTC**; all period math converts through the tenant's IANA zone as a civil date
  first, so DST never moves a period boundary off local midnight.
- **The engine is stateless** — no "last run" marker. Every tick re-derives the ledger from data,
  which makes catch-up and idempotency the same code path. Inserts are guarded by a unique index on
  `(ResponsibilityId, PeriodStart)`.
- **Transitions are explicit endpoints**, never a status `PATCH`. The matrix lives in one file.
- **Lateness comes from the period, not the status.** The engine ticks on a timer; an occurrence
  finished just after its period ended is still `Open` when the user clicks, and recording that as
  on-time would erase a miss on a timer boundary. The insight reports extend the same rule to the
  compliance denominator: a period whose end has passed is judged now, not at the next tick.
- **Insights are computed on read, never precomputed.** Three years of a busy 30-person tenant is
  around 50 000 occurrence rows; SQL narrows them and the bucketing happens in C#, because a
  tenant-local ISO week is not portably derivable from a UTC timestamp on both providers. If a p95
  insight response ever passes a second on real data, the answer is a nightly summary table — not a
  lookback window that would change what the numbers mean.
- **Hold history is reconstructed from `WorkItemEvents`**, pairing each entry into `OnHold` with the
  next exit from it. The reason lives in the entry event's payload because the column on the item is
  cleared the moment the hold is released — which is why hold aging can answer for history nobody was
  recording for reports.
- **CSRF posture**: same-origin SPA + `SameSite=Strict` HttpOnly cookie + no CORS. No token machinery.

**Security and performance**, in one line each: data-protection keys live beside the database so a
restart never signs everyone out; a CSP with no inline script; sign-in answers identically for a wrong
password, an unknown account and a deactivated one; query counts are flat rather than per-row (a
100-item board is 3 queries whether the table holds 36 rows or 800). The reasoning for all of it is in
[docs/architecture.md](docs/architecture.md).

---

## Development

New here? **[docs/testing.md](docs/testing.md)** has four ways to run it plus a five-minute walkthrough.

```bash
# API on http://localhost:5080
dotnet run --project src/Server

# SPA on http://localhost:5173, proxying /api to the API
cd src/client && npm install && npm run dev
```

```bash
dotnet test --project src/Server.Tests/Everdue.Server.Tests.csproj    # PostgreSQL half skips without Docker
cd src/client && npm run check:i18n && npm test && npm run build
```

Set `EVERDUE_TESTS_SKIP_POSTGRES=1` to force the PostgreSQL half to skip; CI runs one leg each way.

The client's API types are generated, never hand-written. CI regenerates them and fails on any
diff, so the checked-in copy cannot drift from the endpoints:

```bash
cd src/client && npm run gen:api      # requires the API running on :5080
```

Adding a migration touches **both** providers:

```bash
cd src/Server
dotnet ef migrations add <Name> --context SqliteEverdueDbContext   -o Infrastructure/Persistence/Migrations/Sqlite
dotnet ef migrations add <Name> --context PostgresEverdueDbContext -o Infrastructure/Persistence/Migrations/Postgres
```

Stay on portable EF Core: LINQ only, no provider-specific SQL. CI runs the whole suite against both
providers so drift is caught the same day it appears.

---

## The guardrail

> **This system manages WORK, not business data.**

It references a customer; it does not store their contracts, invoices or pipeline. It references
equipment; it is not an asset register. The litmus test for any feature request is whether it helps answer
*"what work needs to happen, what happened, and what requires attention?"*

The bounded custom fields are the one place this bends, and they are bent on purpose and no further: capped,
typed, display-only, and unable to drive any behaviour. The moment a field on an entity *does* something, the drift
has begun.

---

## Documentation

Three audiences, and an index at **[docs/README.md](docs/README.md)**.

| For | Where |
|---|---|
| **Using Everdue** | the manual is **inside the app** — sign in and open **Help**, in English or Spanish. Its source is [`src/client/src/features/help/content/`](src/client/src/features/help/content) |
| **Running an install** | [docs/operations.md](docs/operations.md) · [docs/configuration.md](docs/configuration.md) |
| **Working on it** | [CONTRIBUTING.md](CONTRIBUTING.md) · [docs/architecture.md](docs/architecture.md) · [docs/testing.md](docs/testing.md) · [docs/api.md](docs/api.md) · [docs/translating.md](docs/translating.md) |

Reporting a vulnerability: **[SECURITY.md](SECURITY.md)** — privately, never in a public issue.

## Contributing

**[CONTRIBUTING.md](CONTRIBUTING.md)** has how to run it, the checks CI runs, and the discipline the
codebase expects: portable EF Core, layer boundaries, i18n in both languages, and a migration per
provider.

---

## Licence

**GNU Affero General Public License v3.0 or later** — see [LICENSE](LICENSE).

In plain terms: Everdue is free to use, self-host, modify and redistribute. If you modify it and let other people
use it **over a network**, AGPL §13 obliges you to offer them the source of your modified version. That is the whole
point of the AGPL rather than the GPL, and it is why the running app shows its licence and a link to its source in
the footer (`GET /api/v1/about`) — a fork should point that at its own repository.

Self-hosting is free and always will be, and the self-hosted build is not a cut-down edition: it is the product.

```
Everdue — operational accountability for small and medium businesses.
Copyright (C) 2026 Everdue contributors

This program is free software: you can redistribute it and/or modify it under the terms of the GNU
Affero General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Affero General Public License for more details.
```
