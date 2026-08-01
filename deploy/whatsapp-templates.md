# WhatsApp message templates to submit to Meta

Everdue sends WhatsApp messages **business-initiated**, which means they must be pre-approved
templates — free text is not an option outside a 24-hour customer-service window, and a staff
reminder will essentially never be inside one.

Submit these five (plus the optional test template) in the WhatsApp Manager of your WhatsApp
Business Account, then put the approved **names** into Everdue under **Settings → Notification
channels → WhatsApp**. Names are configuration, so an approval that lands later needs no deploy.

## Before you start

- You need a WhatsApp Business Account, a dedicated sending number, and a Meta Business account.
- Category: **Utility** for all of them. Marketing costs several times more and is the wrong
  category for "your task is due".
- Language: submit in the language you configure as `templateLanguage`. Meta's language code is
  per-template — a template approved as `es` cannot be sent as `en`, so if you need both, submit
  both and run two configurations.
- Everdue is billed **per message delivered**. Utility messages are cheap (roughly US$0.004 in the
  US at the time of writing) but not free.

## The variables

Every template takes the **same three body variables**, in the same order, so all five have an
identical shape:

| Variable | Meaning |
|---|---|
| `{{1}}` | The work item's title |
| `{{2}}` | Context — the entity name, or the due date, or `—` when there is neither |
| `{{3}}` | Who did it (the person who assigned, mentioned or put on hold), or `Everdue` |

## The templates

### `everdue_assigned` — someone gave you work

- **es**: `{{3}} te asignó: {{1}} ({{2}})`
- **en**: `{{3}} assigned you: {{1}} ({{2}})`

### `everdue_due_today` — the daily reminder

- **es**: `Vence hoy: {{1}} ({{2}})`
- **en**: `Due today: {{1}} ({{2}})`

### `everdue_missed` — an occurrence went missed

- **es**: `Incumplida: {{1}} ({{2}})`
- **en**: `Missed: {{1}} ({{2}})`

### `everdue_mentioned` — mentioned in a comment

- **es**: `{{3}} te mencionó en: {{1}} ({{2}})`
- **en**: `{{3}} mentioned you on: {{1}} ({{2}})`

### `everdue_on_hold` — somebody put your work on hold

- **es**: `{{3}} puso en espera: {{1}} ({{2}})`
- **en**: `{{3}} put on hold: {{1}} ({{2}})`

### `everdue_test` — optional, for the "send me a test" button

- **es**: `Mensaje de prueba de Everdue. Si lo recibiste, el canal funciona. ({{1}} · {{2}} · {{3}})`
- **en**: `Everdue test message. If you got this, the channel works. ({{1}} · {{2}} · {{3}})`

## What to expect

- A template whose name is **not** configured in Everdue is simply skipped — that person falls back
  to their other channel rather than seeing a failure. So a partial rollout is safe: configure the
  names as approvals arrive.
- A rejected or renamed template shows up in **Settings → Notification channels → Delivery health**
  as a failure with Meta's own error (`132001` = the template does not exist in that language).
  These are not retried, because every message of that type would fail identically.
- **There are no delivery receipts.** Everdue does not run a webhook, so a delivery marked *sent*
  means Meta accepted it — not that it reached a phone. Telegram, which does fail loudly, is the
  channel to trust when you need to know something arrived.

## Recipients

Everdue does not have a WhatsApp linking flow (that would need a public webhook). An administrator
enters each person's number in E.164 form on their user record. Make sure the people concerned have
agreed to be messaged there — for staff that is an employment matter, not something the product can
establish for you.
