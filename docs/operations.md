# Running Everdue

For whoever installs it, backs it up and gets woken up by it. Settings are in
[configuration.md](configuration.md); this is what to *do*.

---

## Install

Four supported shapes. All of them are one process serving the API and the web app on one port.

### Docker

```bash
git clone https://github.com/shernandezp/everdue && cd everdue
cp deploy/.env.example deploy/.env        # edit EVERDUE_ADMIN_EMAIL / EVERDUE_ADMIN_PASSWORD
docker compose -f deploy/docker-compose.yml up -d --build
```

State lives in the `everdue-data` volume. Open <http://localhost:8080>, sign in with the bootstrap
admin, and choose a new password when asked.

### Demo, to look at it with data in it

```bash
docker compose -f deploy/docker-compose.demo.yml up --build
```

Six months of believable history: on-time completions, late ones, real misses, one chronically-missed
responsibility, holds across several reasons, checklists, a photo-proof rule and one-off tasks. The
passwords are public and it lives in its own volume; the seeder **refuses outright** on a database
that already contains data, so it cannot damage a real install.

### Single binary

Download the archive for your platform from the GitHub Releases page and unpack it, or build one
yourself:

```powershell
./deploy/publish.ps1 -Runtime win-x64        # or linux-x64, linux-arm64, osx-arm64
cd publish/win-x64
./Everdue.Server
```

One self-contained executable plus `appsettings.json` and `wwwroot`. No .NET runtime to install; the
database and keys appear in `data/` beside it. Untrimmed on purpose — trimming breaks EF Core, and
~90 MB is the price of "copy one folder and run it".

With nothing configured it listens on **<http://localhost:5000>** — Kestrel's default when no URL is
given. To choose the address, set `ASPNETCORE_URLS` (or the `EVERDUE_`-prefixed equivalent,
`EVERDUE_URLS`) before starting it:

```bash
# Reachable from other machines on the LAN, on port 8080:
ASPNETCORE_URLS=http://0.0.0.0:8080 ./Everdue.Server
```

### As a service

```powershell
# Windows, elevated
./deploy/install-windows-service.ps1 -Path 'C:\Everdue\Everdue.Server.exe'
```

```bash
# Linux
sudo cp -r publish/linux-x64/* /opt/everdue/
sudo cp deploy/everdue.service /etc/systemd/system/
sudo systemctl enable --now everdue
```

---

## First run

On a fresh database the app applies migrations, seeds the tenant and creates the bootstrap admin —
all idempotent, and repeated safely on every start.

1. Set `Tenant:TimeZoneId` **before** anyone creates responsibilities. Every period boundary and due
   date is computed in that zone.
2. Set `Bootstrap:AdminEmail` and `Bootstrap:AdminPassword` if you want to choose the first account.
   Without them, an admin **`admin@everdue.local`** is created with a random password **printed once
   in the startup log**, in a banner you cannot miss.
3. Sign in, change the password when prompted, then set the organisation name, digest hour, reminder
   hour and default language under **Administration → Settings**.

The startup log always names the data directory, so it is never a mystery:

```
info: Data directory: /var/lib/everdue
```

---

## Backing up

**The whole state is the data directory**, not just the database file:

```
data/
  everdue.db      the database (plus -wal / -shm while running)
  keys/           data-protection keys — auth cookies and channel credentials
  files/          attachment bytes
```

| If you lose | Then |
|---|---|
| `everdue.db` | everything |
| `keys/` | everyone is signed out, channel credentials must be re-entered, **and webhook signing secrets must be regenerated** |
| `files/` | attachment records point at bytes that are gone |

Back up the directory. With SQLite, the safest copy is taken with the service stopped; a snapshot of a
running WAL database can be taken with `sqlite3 everdue.db ".backup ..."` if you cannot stop it.

For PostgreSQL, back the database up as you normally would — and **still** back up `keys/` and
`files/`, which do not live in it.

---

## Upgrading

1. Stop the app.
2. **Copy the data directory.**
3. Start the new binary. Migrations apply on startup.

One note for SQLite: a migration that drops a column is implemented as a table rebuild, which EF logs
a warning about because it cannot run inside a transaction. The one place it has happened concerned a
single-row table, so the window is microseconds — but the copy in step 2 is the difference between a
non-event and an afternoon.

Nothing needs configuring afterwards. A version that adds a channel behaves exactly as the previous
one until somebody configures a channel.

---

## Monitoring

| | |
|---|---|
| Liveness | `GET /health` |
| Delivery health | **Administration → Notification channels** — pending, failed in the last 24 h, and the last error per channel |
| Webhook health | **Administration → Settings → Webhooks tab** — pending, failed in 24 h, last error, and whether a subscription auto-disabled |
| Engine | the tick logs one line whenever it creates or misses anything: `Occurrence tick: 3 created, 1 marked missed, 0 skipped` |

Nothing here needs a metrics stack. If you have one, `/health` and the process's own logs are the two
things worth wiring.

---

## Where the data lives in each shape

| How you run it | Data directory |
|---|---|
| `dotnet run --project src/Server` (Development) | `src/Server/data` — deliberately **outside** `bin/`, so it survives `dotnet clean`, a rebuild, and Debug↔Release switches |
| The published binary | `data/` next to the executable — for an install, the app folder *is* the install |
| Docker | the `everdue-data` volume |

---

## When something looks wrong

| Symptom | Cause and fix |
|---|---|
| **Correct password refused, no error** | `Security:RequireHttps` is on behind a plain-HTTP proxy: the browser will not send a `Secure` cookie over HTTP. Turn it off, or terminate TLS at Everdue |
| **Everyone is signed out after every restart** | `DataDir/keys` is not persistent (a container without a volume). Mount it |
| **Nobody can sign in on a fresh install** | `Bootstrap:AdminEmail`/`AdminPassword` were not set, so the first start generated `admin@everdue.local` and printed its password **once**, in that start's log. Find the banner in the log, or set the bootstrap values and start over with an empty data directory |
| **`429` on sign-in** | `Security:LoginAttemptsPerMinute`, per client address. Wait a minute; raise it if a proxy collapses your office onto one address |
| **No occurrences appear** | The responsibility is paused, deactivated, or its start date is in the future. Otherwise check `Engine:Enabled` and the tick log |
| **A pile of misses appeared out of nowhere** | A responsibility was created with a start date in the past: the engine fills in every period between then and now, and those are real misses. Working as designed — see the manual's note on start dates |
| **Notifications are recorded but never arrive** | Either `Notifications:Enabled` is off, or no channel is configured — check the channel health table. An unconfigured channel is *not offered*, never broken |
| **Telegram never links** | `Telegram:PollingEnabled` is off, or the bot token is wrong. There is no webhook to check — linking is long-polled on purpose |
| **A webhook subscription stopped** | Ten consecutive failures auto-disable it. Fix the receiver, then re-enable it in the app |
| **Export returns 400** | Over `Exports:MaxRows`. Narrow the filters — Everdue will not hand you a silently shortened file |
| **The page loads blank** | The SPA was not built into the server. `dotnet publish` does it; a bare `dotnet build` does not unless you pass `-p:BuildClient=true` |
| **Port already in use** | An earlier run is still going |

---

## Resetting a test install

Nothing the app does on its own ever deletes your data — no background job, no upgrade, no migration.
There is exactly **one** operation that does, and an administrator has to ask for it twice: **demo
mode**, on the settings screen. See below.

To start over **deliberately** from outside the app, stop it and delete the data directory; it is
recreated, migrated and re-seeded on the next start:

```powershell
Remove-Item -Recurse -Force src\Server\data
```

That removes the data-protection keys too, so everyone is signed out — which is the point.

### Demo mode, from inside the app

**Settings → Demo mode** lets an administrator fill the workspace with six months of invented history,
and clear it again afterwards. **Both directions delete everything**: every work item, occurrence,
responsibility, entity, department, attachment (bytes included) and user account except the
administrator running it. There is no undo — the only recovery is the backup above, so **take one
first**.

It asks for the workspace name typed out exactly and the caller's own password, refuses API keys
outright, and is administrator-only.

It also **ships disabled**: with `Demo:AllowReset` at its default of `false` the endpoint answers
`404` and the card is never rendered. To evaluate demo mode on a running install, turn it on
deliberately:

```json
{ "Demo": { "AllowReset": true } }
```

`Demo:Seed` — the startup seeder for a fresh demo install — works either way and still refuses any
database that already holds data.
