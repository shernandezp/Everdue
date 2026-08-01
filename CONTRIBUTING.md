# Contributing to Everdue

Thanks for looking. Everdue is a small, deliberately narrow product; the most useful contribution is usually a
report of something that does not work in your installation, or a translation.

Everdue is licensed under the **GNU AGPL v3.0 or later**. By contributing you agree that your contribution is
licensed the same way. There is no CLA.

---

## The one rule that shapes everything

> **This system manages WORK, not business data.**

It *references* a customer; it does not store their contracts, invoices or pipeline. It *references* equipment;
it is not an asset register. The litmus test for any feature is whether it helps answer
*"what work needs to happen, what happened, and what requires attention?"*

Features that fail that test are declined, however reasonable they are individually — that is the whole reason
Everdue is simpler than an ERP. If you are unsure, open an issue before writing code.

The reasoning behind each boundary is in [docs/architecture.md](docs/architecture.md) and in the code's own
comments — the codebase explains *why* far more than *what*, and that is deliberate.

---

## Running it

```bash
# API on http://localhost:5080
dotnet run --project src/Server

# SPA on http://localhost:5173, proxying /api to the API
cd src/client && npm install && npm run dev
```

Want it full of data to look at? See **[demo mode](README.md#demo-mode)**.

## Running the checks

Everything CI runs, you can run:

```bash
dotnet test src/Server.Tests/Everdue.Server.Tests.csproj    # the PostgreSQL half skips without Docker
cd src/client && npm run check:i18n && npm test && npm run build
```

`EVERDUE_TESTS_SKIP_POSTGRES=1` forces the PostgreSQL half to skip. **You do not need Docker to contribute** —
CI runs one leg each way, which also proves the product works on a machine that has none.

---

## Discipline the codebase expects

These are not style preferences; each one exists because breaking it caused a real bug.

### Stay portable across both database providers

Everdue runs on **SQLite and PostgreSQL from one codebase**. That means:

- **LINQ only.** No raw SQL, no provider-specific functions, no `IncludeProperties`, no JSON predicates pushed
  to the database. Deserialize JSON in memory instead.
- **A migration per provider**, always both:

  ```bash
  cd src/Server
  dotnet ef migrations add <Name> --context SqliteEverdueDbContext   -o Infrastructure/Persistence/Migrations/Sqlite
  dotnet ef migrations add <Name> --context PostgresEverdueDbContext -o Infrastructure/Persistence/Migrations/Postgres
  ```

- Bucketing, rate arithmetic and date maths happen **in C#**, not in SQL: a tenant-local ISO week is not
  portably derivable from a UTC timestamp on both providers.

### Respect the layer boundaries

Clean Architecture as folder boundaries inside `src/Server`:

- `Domain` references **nothing** — no EF, no ASP.NET, no DI.
- `Application` reaches persistence only through `IEverdueDbContext`, and never touches `Infrastructure`.
- Endpoints **bind and dispatch**; they never decide. Rules live in handlers.
- DTOs are never EF entities.

### Keep files and classes small

One responsibility per file. If a handler is growing past ~120 lines, something in it belongs elsewhere. If you
need a shared reader or rule, give it its own file rather than a `Helpers` class.

### Time, tenancy and the ledger

- All timestamps are UTC `DateTimeOffset`; every period and due date converts through the tenant's IANA zone.
- Every tenant-owned table carries `TenantId` and is covered by the one global query filter. If you find yourself
  writing `IgnoreQueryFilters()`, you are doing something that needs a comment explaining why.
- **A miss is never erased.** Nothing may make a recorded miss disappear, and no new status may quietly drop rows
  out of a compliance count. This is the product.

### Every user-visible string goes through i18n

`npm run check:i18n` fails CI on a missing key or a locale mismatch. No hardcoded UI text.

API error messages and log output stay **English** on purpose — they are developer-facing.

### A change a user can see changes the manual

The in-app manual lives at `src/client/src/features/help/content/{en,es}/` as markdown, one file per
topic per language. A new screen or a changed option means editing it — **in both languages, in the
same pull request**. Use the words the interface uses, taken from the locale files rather than from a
dictionary. See [docs/user-manual.md](docs/user-manual.md).

### No new dependency without a reason in the pull request

Say what it does, what it replaced, and what its licence is. Everdue publishes as a single self-contained binary;
each dependency is weight a self-hoster carries.

---

## Adding a language

A translation is data: see **[docs/translating.md](docs/translating.md)**. It is four files, one of which fails
silently if you miss it — a test catches that, and the doc says which one.

---

## Pull requests

- Branch from `main`, keep the change focused, and say what problem it solves.
- The four CI jobs must be green: client (i18n + tests + build), API contract, server × {SQLite, PostgreSQL},
  Docker image.
- If you changed an endpoint, `docs/openapi/v1.json` changes with it:

  ```bash
  dotnet build src/Server/Everdue.Server.csproj -p:GenerateOpenApi=true
  cd src/client && npm run gen:api
  ```

  CI fails if the committed contract and the code disagree. That diff is deliberate: it is how a breaking change
  becomes visible in review. See the compatibility policy in [docs/api.md](docs/api.md).
- Tests: a bug fix comes with a test that would have caught it; a new rule comes with a test that pins it.

## Reporting a vulnerability

**Not in a public issue.** See [SECURITY.md](SECURITY.md).
