# Configuration

Everything is `appsettings.json`, overridable by environment variables and command-line arguments:

```
appsettings.json  <  appsettings.{Environment}.json  <  environment variables  <  command line
```

An environment variable spells a nested key with a **double underscore**:
`Tenant:TimeZoneId` → `Tenant__TimeZoneId`.

Two things behave differently from the framework's defaults, on purpose:

- The app reads `appsettings.json` and serves the SPA **from beside its own executable**, not the
  working directory, so it behaves identically started by hand, by `sc.exe`, or by systemd.
- Relative paths (`DataDir`) resolve against the binary for the same reason.

---

## The settings most installations touch

| Setting | Default | What it does |
|---|---|---|
| `Tenant:TimeZoneId` | `UTC` | **IANA** id, e.g. `America/Bogota`. Every period boundary and due date is computed in this zone. Get this right before creating responsibilities |
| `Bootstrap:AdminEmail` / `AdminPassword` | *(empty)* | The first-run administrator. A password change is forced at first sign-in. On a fresh database with these unset, an admin **`admin@everdue.local`** is generated with a random password **printed once in the startup log** — sign in with it and change it when prompted |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres` |
| `ConnectionStrings:Default` | *(empty)* | Empty + SQLite ⇒ `DataDir/everdue.db` with WAL. Required for Postgres |
| `DataDir` | `./data` | Database, data-protection keys **and attachment files**. This directory is the whole backup |
| `Tenant:DefaultLanguage` | `es` | `es` or `en`. A user's own preference overrides it |
| `Security:RequireHttps` | `false` | Marks the auth cookie `Secure` and sends HSTS. Turn it **on** when TLS terminates at Everdue; leave it **off** behind a plain-HTTP reverse proxy, or the browser drops the cookie and logins fail silently with correct credentials |
| `App:PublicBaseUrl` | *(empty)* | Where this install is reachable, used to put a link in a notification. Empty means messages carry no link, which beats carrying a broken one |

---

## Schedule

| Setting | Default | What it does |
|---|---|---|
| `Engine:Enabled` | `true` | The occurrence engine. Off means nothing spawns and no miss is recorded |
| `Engine:TickMinutes` | `5` | Tick interval. It also runs once at startup |
| `Engine:MaxOccurrencesPerResponsibilityPerTick` | `5000` | Safety bound on catch-up for one responsibility in one tick |
| `Tenant:DigestHourLocal` | `7` | Local hour the manager digest is sent. A **first-run seed value**: once the tenant exists it is edited in the app, not here |
| `Digest:Enabled` / `CheckMinutes` | `true` / `10` | The digest service and how often it checks whether the local hour has arrived |
| `Reminders:Enabled` / `CheckMinutes` | `true` / `10` | The same, for reminders |

The hour the "due today" reminders go out is **not** a config key: it lives on the tenant and is set
in the app under **Administration → Settings** (default 8 — after the digest, because managers read
before the day starts and the people doing the work want it once they have). The digest hour is
edited in the same place after first run.

## Notifications and channels

| Setting | Default | What it does |
|---|---|---|
| `Notifications:Enabled` | `true` | The outbox dispatcher. **Off = notifications are still recorded and readable in the app**; nothing leaves the machine |
| `Notifications:DispatchSeconds` | `30` | How often the outbox is drained |
| `Notifications:MaxAttempts` | `5` | Retry cap per delivery. Backoff is exponential, capped at an hour |
| `Notifications:RetentionDays` | `90` | How long **read** notifications are kept. They are not the ledger — `WorkItemEvents` is, and it is never swept |
| `Notifications:BatchSize` | `100` | Deliveries per pass; also the pacing bound |
| `Notifications:MissedNotificationWindowHours` | `24` | Misses older than this are recorded but not announced. Without it, a fortnight of downtime would flip hundreds of occurrences on one tick and message every one of them |
| `Telegram:PollingEnabled` | `true` | Long polling for the account-link flow. **No webhook**, so an install behind NAT works. Turn it off on an install that only sends |
| `Telegram:PollTimeoutSeconds` | `30` | Long-poll timeout |
| `Smtp:Host` / `Port` / `User` / `Password` / `From` / `FromName` / `UseStartTls` | *(empty)* / `587` / … | Optional. Acts as the **system-scope** e-mail channel when a tenant has configured none of its own, so an upgraded install keeps sending exactly as it did |

Per-tenant channel credentials are configured in the app (**Administration → Notification channels**),
encrypted with the key ring in `DataDir/keys`, and take precedence over the settings above.

## Security and authentication

| Setting | Default | What it does |
|---|---|---|
| `Security:RequireHttps` | `false` | See above |
| `Security:LoginAttemptsPerMinute` | `30` | Sign-in attempts per client address per minute. Defence against password spraying, which account lockout cannot see. Raise it if a proxy collapses your whole office onto one address |
| `Security:ApiRequestsPerMinute` | `600` | Per API key, fixed window, `429` when exceeded. Cookie sessions are never rationed because a script was busy |
| `Auth:Google:ClientId` / `ClientSecret` | *(empty)* | Sign in with Google. Empty ⇒ the button is not rendered and the routes 404. Requires `Security:RequireHttps` and a stable public host; register `https://{host}/signin-google` as the redirect URI |

Fixed and not configurable: passwords need 10 characters with mixed case and a digit; an account locks
for 15 minutes after 10 failures; auth cookies revalidate every 2 minutes so a deactivated user loses
access promptly.

## Limits

| Setting | Default | What it does |
|---|---|---|
| `Attachments:MaxSizeBytes` | `10485760` (10 MB) | Per file |
| `Attachments:MaxPerWorkItem` | `10` | Per work item |
| `Attachments:AllowedContentTypes` / `AllowedExtensions` | images, PDF, office documents, plain text | Both are checked; both must pass |
| `Checklists:MaxItemsPerTemplate` / `MaxItemsPerWorkItem` | `50` / `50` | Steps per template and per item |
| `EntityFields:MaxPerEntityType` | `10` | The cap that keeps custom fields from becoming a schema |
| `Exports:MaxRows` | `50000` | Report and insight exports over this return **400 with an instruction**, never a shorter file. Raw table dumps are streamed and uncapped |
| `Import:MaxRows` / `MaxSizeBytes` / `PreviewRows` / `MaxReportedFailures` | `5000` / `2 MB` / `20` / `1000` | The CSV import wizard |

## Insights

| Setting | Default | What it does |
|---|---|---|
| `Insights:ChronicMissCount` / `ChronicWindowPeriods` | `3` / `8` | "Chronically delayed" means K misses in a responsibility's last N concluded periods. Two integers, deliberately not a rule engine |
| `Insights:MinOccurrencesForRate` | `5` | Below this many concluded periods a percentage is **withheld** and only the raw count shown. Rates on tiny denominators mislead |
| `Insights:DefaultTrendBuckets` / `MaxTrendBuckets` | `12` / `52` | Columns on a trend axis by default, and the ceiling. A wider range is refused by name rather than silently truncated |
| `Insights:TopEntities` | `15` | Rows kept in by-entity lists. What the cap dropped is always reported |

## Webhooks

| Setting | Default | What it does |
|---|---|---|
| `Webhooks:Enabled` | `true` | The outbound dispatcher |
| `Webhooks:DispatchSeconds` | `15` | Drain interval |
| `Webhooks:MaxAttempts` | `5` | Retries per delivery, exponential backoff capped at an hour |
| `Webhooks:MaxConsecutiveFailures` | `10` | After this many in a row the subscription is **auto-disabled** until an administrator re-enables it |
| `Webhooks:MaxSubscriptions` | `10` | Per tenant |
| `Webhooks:TimeoutSeconds` | `10` | Receivers must answer quickly and queue their own work |
| `Webhooks:RetentionDays` | `30` | How long delivery rows are kept |
| `Webhooks:AllowInsecureUrls` | `false` | `https` is required unless this is set — it exists for a localhost receiver during development. Private and loopback addresses are allowed either way, by design |

## Demo mode

| Setting | Default | What it does |
|---|---|---|
| `Demo:Seed` | `false` | Seeds six months of believable history **at startup**. It **refuses outright** on a database that already contains data, so it cannot damage a real install |
| `Demo:Password` | `EverdueDemo2026!` | Public by design; demo mode only |
| `Demo:Months` | `6` | How much history to build |
| `Demo:AllowReset` | `false` | May an administrator switch demo mode on or off from **Settings** in the running app? Off by default; see below |

`deploy/docker-compose.demo.yml` turns `Demo:Seed` on for you.

### The runtime switch, and why it ships off

`Demo:Seed` only fires on an empty database at startup, which is no help to somebody who has already
started using Everdue and wants to see what the reports look like with data in them. So administrators
can also get a **Demo mode** card on the settings screen — when `Demo:AllowReset` is turned on.

> **Both directions delete everything.** Turning demo mode *on* wipes the workspace and writes six
> months of invented history over it. Turning it *off* wipes the workspace and leaves it empty, ready
> for real use. In both cases every work item, occurrence, responsibility, entity, department,
> attachment (including the files on disk) and user account **except the administrator performing it**
> is deleted. There is no undo. The only recovery is the backup of the data directory.

It is guarded by four things: the caller must be an administrator, must hold a cookie session (an API
key is refused outright — no script can wipe a tenant), must type the workspace name exactly, and must
re-enter their own password.

**It ships off.** With `Demo:AllowReset` at its default of `false` the endpoint answers `404`, the card
is never rendered, and the capability simply is not there — which is a stronger guarantee than a
confirmation dialog. To evaluate demo mode on a running install, set `Demo:AllowReset=true`
(`Demo__AllowReset=true` as an environment variable) and restart; `deploy/docker-compose.demo.yml`
already does. `Demo:Seed` works either way for provisioning a fresh demo install.

While demo mode is on, every signed-in user sees a **Demo data** badge in the header, whatever their
role. Seeded history is deliberately indistinguishable from real history — that is what makes the demo
worth looking at — so the badge is the only thing that stops somebody filing real work into it.

## Advanced

| Setting | Default | What it does |
|---|---|---|
| `Database:MigrateOnStartup` | `true` | Apply pending migrations at startup. Always true for a self-hosted install; the test suite turns it off |

---

## Worked example: a small install behind a reverse proxy

```jsonc
{
  "DataDir": "/var/lib/everdue",
  "Database": { "Provider": "Sqlite" },
  "Tenant": {
    "Name": "Acme Distribution",
    "TimeZoneId": "America/Bogota",
    "DefaultLanguage": "es",
    "DigestHourLocal": 7
  },
  "App": { "PublicBaseUrl": "https://everdue.acme.example" },
  // TLS terminates at nginx, so the cookie must NOT be marked Secure here.
  "Security": { "RequireHttps": false },
  "Smtp": {
    "Host": "smtp.acme.example",
    "From": "everdue@acme.example",
    "User": "everdue@acme.example",
    "Password": "…"
  }
}
```

The same file as environment variables, for compose:

```yaml
environment:
  DataDir: /data
  Tenant__TimeZoneId: America/Bogota
  Tenant__DefaultLanguage: es
  App__PublicBaseUrl: https://everdue.acme.example
  Smtp__Host: smtp.acme.example
  Smtp__From: everdue@acme.example
```
