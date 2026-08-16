# Running and testing Everdue locally

You need the **.NET 10 SDK** and **Node 24**. Pick one of the four ways below; the first is the
quickest way to click around, and the last is the quickest way to see the product with data in it.

---

## 1. Fastest: one process, real production shape

Builds the client into the server and runs the whole thing on one port — exactly what a self-hosted
install looks like.

```bash
dotnet build src/Server -p:BuildClient=true
dotnet run --project src/Server
```

Open <http://localhost:5080> and sign in:

| | |
|---|---|
| E-mail | `admin@everdue.local` |
| Password | `ChangeMe2026!` |

It immediately asks you to choose a new password — that is the forced first-run change. Pick anything
with 10+ characters, upper and lower case, and a digit.

> The seeded account comes from `appsettings.Development.json` and only exists in development.

## 2. Working on the UI: hot reload

Two terminals. The Vite dev server proxies `/api` to the backend, so it stays same-origin and the auth
cookie behaves exactly as in production.

```bash
# terminal 1
dotnet run --project src/Server

# terminal 2
cd src/client && npm install && npm run dev
```

Open <http://localhost:5173>. Same login. Edits to `.tsx` files appear instantly.

## 3. The real install: one self-contained binary

What a customer would receive — no .NET runtime required on the target machine.

```powershell
./deploy/publish.ps1 -Runtime win-x64
cd publish/win-x64

$env:Bootstrap__AdminEmail    = 'you@yourcompany.com'
$env:Bootstrap__AdminPassword = 'FirstRunPass2026!'
$env:Tenant__TimeZoneId       = 'America/Bogota'
$env:ASPNETCORE_URLS          = 'http://localhost:5099'
./Everdue.Server.exe
```

One executable plus `appsettings.json` and `wwwroot`; the database and encryption keys appear in
`data/` next to it. Copy that folder anywhere and it still runs.

The `Bootstrap__*` variables are optional: without them the first start generates
`admin@everdue.local` with a random password printed once in the log. Without `ASPNETCORE_URLS` it
listens on <http://localhost:5000>.

## 4. Demo mode: six months of history, one command

```bash
docker compose -f deploy/docker-compose.demo.yml up --build
```

Sign in as `ana@demo.everdue.app` / `EverdueDemo2026!`. Every report and insight screen has something
on it, which an empty install cannot show you.

---

## A five-minute walkthrough

Signed in as the administrator:

1. **Entities → New entity.** Add a customer, e.g. *Acme Distribution*.
2. **Departments → New department.** Add *Operations*.
3. **Users → New user.** Create a Member (say *María*) so you can see the non-admin view later.
4. **Responsibilities → New responsibility.**
   - Title: *Weekly follow-up with Acme*
   - Owner: yourself · Entity: Acme · Repeats: **Weekly**, tick **Mon**
   - **Starts on: pick a date about three weeks ago.**
5. **Restart the app.** The occurrence engine ticks at startup (and every 5 minutes after).
6. **My work.** You should now see roughly **3 missed occurrences and 1 to do** — the ledger filled in
   the past on its own. That is the core behaviour: a missed period is recorded, never erased, and the
   next occurrence spawns anyway.
7. Drag the **To do** card to **In progress**, then to **Done**. Dragging straight from To do to Done
   also works — nobody should have to click twice.
8. Drag a **Missed** card to **Done** — it lands as *Completed late*, and the miss stays on the record.
   Check entity health afterwards: the 30-day missed count does not drop.
9. Try dragging a **Missed** card to **In progress** — refused. A miss stays visible until it is
   completed late, or it would leave the compliance counts while somebody worked on it.
10. Drag a card to **On hold** — it forces a reason. Choose *Other* and try to confirm with no text.
11. Try dragging anything into **Missed** — refused. Only the engine records a miss.
12. Use the **owner picker** at the top of the board: your work → everyone → one person. That is the
    manager view.
13. **Exceptions dashboard.** Click any number; it opens the exact list behind it.
14. **Entity health** → click *Acme* for its full timeline.
15. Sign out, sign in as *María*: board and entity screens only, no dashboard and no admin.

### Things worth poking at

- **Language.** Top-right menu → Profile → switch English/Español. Every screen changes.
- **Appearance.** The app follows your system theme by default; override it under the top-right menu.
- **Help.** The in-app manual, in the same language as the rest of the app.
- **Time zone.** Settings → change it, then create a responsibility; due dates follow the new zone.
- **Password reuse.** On the forced first change, try re-entering the same password — refused.
- **Nothing is lost on restart.** Stop and start the app: you stay signed in and the ledger is identical.
- **Rate limiting.** Fail the login 30+ times in a minute and you get `429`.

---

## The test suites

```bash
dotnet test --project src/Server.Tests/Everdue.Server.Tests.csproj   # the PostgreSQL half skips without Docker
cd src/client
npm run check:i18n    # both languages complete, no hardcoded strings
npm test              # component tests
npm run build         # type-check + production build
```

`EVERDUE_TESTS_SKIP_POSTGRES=1` forces the PostgreSQL half to skip. **You do not need Docker to
contribute** — CI runs one leg each way, which also proves the product works on a machine that has none.

| Suite | Pins |
|---|---|
| `Recurrence/` | every recurrence kind, month-end clamping, DST boundaries, tenant-local dates |
| `Engine/` | spawning, catch-up from downtime, idempotency, pause/resume, missed flipping, digest content |
| `Api/` | the real pipeline: transitions, tenancy, query filters, report and insight correctness, checklists, completion proof, export/import round trips, API keys, webhooks, external login, security headers |
| `Notifications/` | trigger rules, channel resolution, the outbox, provider clients, Telegram linking |
| `Domain/` | the transition matrix, insight maths, localisation resources |
| client | board drag rules, checklist panel, hold dialog, notification list, insight components, locale parity |

Server tests build their fixtures with the harnesses in `Server.Tests/Support` — `EverdueApp`,
`LedgerBuilder`, `EngineHarness`, `TestClock`. Time is always injected; no test reads the wall clock.

---

## Where your data lives (and how to reset it)

The app **never** deletes your database — migrations and seeding are idempotent and run on every
start. Every run logs the location:

```
info: Data directory: .../src/Server/data
```

| How you run it | Data directory |
|---|---|
| `dotnet run --project src/Server` (Development) | `src/Server/data` — deliberately **outside** `bin/`, so it survives `dotnet clean`, a rebuild, and Debug↔Release switches |
| The published binary | `data/` next to the executable |
| Docker | the `everdue-data` volume |

To start over deliberately, stop the app and delete the folder; it is recreated, migrated and
re-seeded on the next start. That also removes the data-protection keys, so everyone is signed out —
which is the point.

## If something goes wrong

| Symptom | Cause |
|---|---|
| Port already in use | An earlier run is still going |
| Login says the credentials are wrong | The database already exists with a different admin, or you already changed the password. Delete the data directory to start over |
| Your test data seems to have vanished | Check the `Data directory:` line in the startup log — you are probably running a different build, which has its own database |
| Page loads but is blank | The client was not built. Use option 1 or 2, not a bare `dotnet run` |
| `429 Too Many Requests` on login | The rate limiter. Wait a minute |

More, for a real installation: [operations.md](operations.md).
