## What this changes

<!-- The problem it solves, in a sentence or two. -->

## Checklist

The four CI jobs are the gate; this is what they check.

- [ ] `dotnet test --project src/Server.Tests/Everdue.Server.Tests.csproj` passes
      *(the PostgreSQL half skips without Docker — CI runs one leg each way)*
- [ ] `cd src/client && npm run check:i18n && npm test && npm run build` passes
- [ ] Any new user-visible string goes through i18next, in **both** locales
- [ ] A schema change has **a migration per provider** (SQLite *and* PostgreSQL), and
      `dotnet ef migrations has-pending-model-changes` is clean on both
- [ ] An endpoint change is reflected in `docs/openapi/v1.json` and `src/client/src/api/schema.d.ts`
      (`dotnet build src/Server/Everdue.Server.csproj -p:GenerateOpenApi=true`, then `npm run gen:api`)
- [ ] A bug fix comes with a test that would have caught it; a new rule comes with a test that pins it

## If this touches the data model or the ledger

- [ ] No query escapes the tenant global query filter (or the exception is commented and justified)
- [ ] Nothing can erase or hide a recorded miss
- [ ] Portable EF Core only — LINQ, no provider-specific SQL, no JSON predicates in the database

## If this adds a dependency

- [ ] Stated what it does, what it replaced, and its licence
