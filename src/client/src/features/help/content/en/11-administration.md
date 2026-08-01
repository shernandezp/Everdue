# Administration

*Administrators only.* Everything in this section is setup: done once, then rarely touched.

## People and roles

**Administration → Users.**

| Role | Can |
|---|---|
| **Member** | Work on the board and the list, read entities, comment, attach, tick checklists — including on colleagues' work |
| **Administrator** | All of that, plus responsibilities, reports, insights and this whole section |

There is **no self-service registration**. You create the account; the person is asked to choose a new
password at their first sign-in.

| Action | Notes |
|---|---|
| **New user** | E-mail, a first password, display name, role, language |
| **Edit** | Name, role, language, WhatsApp number, active flag |
| **Reset password** | Sets a new one and forces a change at next sign-in |
| **Hand over** | The departure path — see below |

**Deactivate rather than delete.** Deactivating removes access within a couple of minutes and keeps
every record intact. Everdue never deletes a person who did something, because the history names them.

### When somebody leaves

Use **Hand over** on their row. Choose who takes over, and whether to move their responsibilities,
their outstanding work, or both. It is one action rather than a list of items, because this is a screen
somebody opens under time pressure.

The history stays with the old owner: "who did what" is never rewritten.

## Organisation settings

**Administration → Settings.**

| Setting | Notes |
|---|---|
| **Name** | Shown in the header |
| **Time zone** | An IANA name like `America/Bogota`. **Every due date and period is computed in this zone** — set it before anyone creates responsibilities |
| **Digest hour** | When the manager digest goes out, local time |
| **Reminder hour** | When "due today" reminders go out. Later than the digest by default: managers read before the day starts, the people doing the work want it once they have |
| **Default language** | For anybody who has not chosen their own |
| **Allow shared channels** | Whether this organisation may fall back to the installation's own e-mail settings |

## Notification channels

**Administration → Notification channels.** One card per channel, each with its own configuration, its
own **Send a test** button and its own health.

| Channel | Needs | Notes |
|---|---|---|
| **E-mail** | SMTP host, sender address, credentials | The simplest to set up |
| **Telegram** | A bot token from Telegram's @BotFather | Recommended for people in the field: free, and needs nothing opened on your network |
| **WhatsApp** | A WhatsApp Business account and templates approved by Meta | Messages are billed. "Sent" means Meta accepted it — there is no read receipt |

Secrets are stored encrypted and never shown again. Leaving a secret blank when you edit keeps the one
already stored, so you can change a bot's username without re-typing its token.

The **health** table at the bottom shows pending, failed in the last 24 hours, and skipped per channel.
That is the first place to look when somebody says they got nothing.

## Custom fields

**Administration → Settings → Custom fields tab.** Up to ten extra fields per entity type — text, number, date, or a
list to choose from.

They are **display-only**: nothing filters, sorts, reports or sends a webhook on them. Deleting a
definition leaves the values ignored rather than deleting anything.

## Importing from a spreadsheet

**Administration → Import.** Three steps, and nothing is written until the last one.

1. **File.** Choose *Entities* or *One-off tasks*, then the CSV. Comma or semicolon separated — what
   Excel exports in Spanish works.
2. **Columns.** Everdue suggests a mapping and shows real rows from your file so you can check it.
3. **Result.** How many were created, how many skipped, and every failure with its row number. A
   failure list can be downloaded as a CSV.

Two guarantees: **an import never overwrites** — a row that already exists is skipped and reported —
and occurrences can never be imported, because those are Everdue's to create from responsibilities.

## API keys

**Administration → Settings → API keys tab.** For a script or an automation platform that needs to read or write work.

- The token is shown **once**. Store it somewhere safe; Everdue keeps only a fingerprint.
- **Read-only** or **read-write**.
- A key acts as a person, so anything it writes is attributed to somebody real.
- A key can only reach work-related endpoints. Even a key created by an administrator **cannot** create
  users or read a channel secret.
- **Revoke** takes effect immediately.

## Webhooks

**Administration → Settings → Webhooks tab.** Everdue calls out to your systems when something happens.

Six events: work item created, completed, missed, put on hold, handed over, and entity created.

- The signing secret is shown **once** when you create the subscription. Your receiver uses it to verify
  each call is genuinely from Everdue.
- **Send a test** queues a `ping` so you can prove a receiver works before relying on it.
- Deliveries retry with increasing gaps. After ten consecutive failures the subscription is
  **automatically disabled** and a banner says so; fix your endpoint, then switch it back on.
- Everdue only calls **out**. Nothing needs to be opened on your network.

Technical detail for whoever builds the receiver: `docs/api.md` in the repository.

## Demo mode

**Administration → Settings**, at the bottom of the page.

Everdue is hard to judge empty. The ledger, the compliance strip and the health table are all invisible
until there is history behind them, so demo mode fills the workspace with **six months of invented
history** — a dozen responsibilities, real customers and machines, checklists, holds, completions and
misses — and every report and insight screen suddenly has something on it.

> **It deletes everything, in both directions.**
>
> Turning it **on** wipes the workspace and writes the demo data over it. Turning it **off** wipes the
> workspace again and leaves it empty, ready for real use.
>
> Either way you lose every work item, occurrence, responsibility, entity, department, attachment and
> user account **except your own**. Your colleagues have to be added again. **It cannot be undone** —
> the only way back is a backup of the data directory.

Because of that, Everdue asks for two things before it will do it: the **workspace name typed out
exactly**, and **your own password**. Only administrators see the card, and no API key can reach it at
all.

While demo mode is on, everybody sees a **Demo data** badge beside the workspace name in the header.
The invented history looks exactly like real history — that is the point of it — so the badge is the
only thing telling your team not to file real work here.

**Trying Everdue out?** Turn demo mode on, look around the reports and insights for as long as you
like, then turn it off and start for real. **Already using Everdue?** Do not turn it on. It will delete
your work.

Your installation's operator can remove this entirely (`Demo:AllowReset`), in which case the card is
not there at all.

## A sensible setup order

1. **Settings** — time zone first, then language and hours.
2. **Departments** — the teams that do the work.
3. **Entities** — customers, suppliers, machines. Import them if you have a list.
4. **Users** — the people, with the right roles.
5. **Responsibilities** — the recurring work, **starting today** unless you deliberately want history.
6. **Channels** — e-mail or Telegram, then send yourself a test.
7. Leave it a week, then open **Insights**. There will be something in it.
