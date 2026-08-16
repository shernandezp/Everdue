# Translating Everdue

A language is **data**. Adding one means adding files — no `.tsx` change, no pass over every screen.

Spanish and English ship in the box. Everything below is what a third language needs.

> **Translate from English.** `en.json` and the `.resx` files without a language suffix are the source texts, even
> though the product's default language is Spanish: contributors read English, and one reference language means one
> place a wording change starts.

---

## The four files, in order

### 1. The app's screens — `src/client/src/i18n/locales/<code>.json`

Copy `en.json` and translate the values. Keep every key, keep the nesting, and keep the `{{placeholders}}` exactly
as they are — `{{count}}` is substituted at runtime and a translated placeholder name silently renders as nothing.

Nothing else in the client changes: locales are discovered with `import.meta.glob`, so the file being there is what
adds it.

### 2. What the server writes — `src/Server/Resources/*.<code>.resx`

Three families, because the server renders text too:

| File | What it is |
|---|---|
| `NotificationStrings.<code>.resx` | notification bodies (e-mail, Telegram, WhatsApp) |
| `DigestStrings.<code>.resx` | the manager digest e-mail |
| `BotStrings.<code>.resx` | the two sentences the Telegram bot says when linking an account |

Copy the un-suffixed file of each (the English neutral set) and translate the values, keeping the `name`
attributes.

### 3. The supported list — `src/Server/Domain/Tenant.cs`

Add the code to `Languages.Supported`, and its own name to `Languages.NativeName` if the framework's `NativeName`
is not what people actually call it:

```csharp
public static readonly string[] Supported = [Spanish, English, "pt"];
```

The server owns this list and serves it from `GET /api/v1/languages`; the client's language pickers render from
that, so a locale file with no entry here is simply not offered.

### 4. The build's culture list — `src/Server/Everdue.Server.csproj`

```xml
<SatelliteResourceLanguages>en;es;pt</SatelliteResourceLanguages>
```

**This is the one that fails silently.** A culture missing from this list has its satellite assembly dropped from
the build output, so the screens are translated and the digest arrives in English. `LanguageResourceTests` fails
when it happens, which is why the test exists.

---

## Checking it

```bash
cd src/client && npm run check:i18n     # key parity across every locale, and every key used in the code resolves
dotnet test --project src/Server.Tests/Everdue.Server.Tests.csproj --filter-class "*LanguageResourceTests"
```

`check:i18n` reads every JSON file in the locales folder, so it picks a new language up on its own. It fails on a
key present in one locale and missing from another, which is what stops a translation going stale silently as the
product grows.

Then run it: sign in, switch your language in **Profile**, and look at a few screens. Automated checks prove the
keys exist; only a person can see that a label no longer fits its column.

---

## What stays in English

- **API error messages and `ProblemDetails`.** Developer-facing.
- **Log output.**
- **The OpenAPI document and `docs/`.**

Dates and numbers are formatted per language automatically; times are always shown in the tenant's configured time
zone, whatever the reader's language.

---

## Things worth knowing before you start

- **Everdue's vocabulary is operational, not software-development.** *Responsibility*, *occurrence*, *missed*,
  *on hold*, *waiting on customer*. Translate toward what a warehouse supervisor or an administrator says, not
  toward project-management jargon.
- **"Missed" is a load-bearing word.** It is not "overdue", not "late", not "pending" — it means a period passed
  and the work did not happen, and it is permanent. Pick a word that carries that.
- **Some strings are deliberately blunt.** "This is the only time this token is shown" is a warning, not
  marketing; keep the register.
- **A pull request per language**, please. It makes review possible and gives you the credit.

Right-to-left layout is not supported yet. An RTL translation is welcome, but expect layout work alongside it.
