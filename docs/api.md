# The Everdue API

Everything is under `/api/v1`. The full contract is **[openapi/v1.json](openapi/v1.json)** — committed to the
repository, generated at build time, and diffed by CI, so it cannot drift from the code. Point any OpenAPI client
generator at it.

A development instance also serves an interactive reference at `/scalar/v1`.

---

## Authenticating

Two ways in. They are not interchangeable.

| | Cookie | API key |
|---|---|---|
| Who | A person using the web app | A script, or an automation platform |
| How | `POST /api/v1/auth/login` | `X-Api-Key: evd_…` header |
| Reaches | Everything their role allows | An allow-list — see below |

There is deliberately **one** header (`X-Api-Key`) and no `Authorization: Bearer` alias: two ways to authenticate
is two things to get wrong, and `Bearer` invites the assumption that a JWT would work.

### Creating a key

Administrators, under **Settings → API keys**. The token is shown **once**; Everdue stores its prefix and a
SHA-256 hash and cannot show it again. Revocation is immediate.

```bash
curl -s https://everdue.example.com/api/v1/workitems?status=Missed \
  -H 'X-Api-Key: evd_1a2b3c4d5e6f_…'
```

### What a key can and cannot reach

A key acts **as a person** — the actor chosen when it was created — so every write it makes lands in the ledger
attributed to somebody real, with the key's id recorded alongside.

What stops that from making a key an admin credential is that reachability is an **endpoint allow-list, not a
role**. A key may use:

`/workitems` · `/entities` · `/departments` · `/responsibilities` · `/comments` · `/attachments` ·
`/workitems/{id}/checklist` · `/responsibilities/{id}/checklist-template` · `/reports` · `/insights` · `/exports`

A key gets **403** on everything else — `/users`, `/settings/*`, `/api-keys`, `/webhooks`, `/imports/*`,
`/notifications`, `/me` — *even when its actor is an administrator*. A leaked key cannot create a user, read a
channel credential, or reach `/settings/demo`, which is the one endpoint in Everdue that deletes data. The public
API can create work; it cannot destroy a tenant.

Scope splits reading from writing: a `ReadOnly` key gets 403 on any method that is not `GET` or `HEAD`.

### Rate limiting

`Security:ApiRequestsPerMinute` (default 600) per key, fixed window, `429` when exceeded. Cookie sessions are not
rate-limited on these endpoints — a person clicking around is never rationed because a script was busy.

---

## Conventions

- JSON in, JSON out; `camelCase` on the wire. Enums travel as **names**, never as numbers.
- Paging is `?page=1&pageSize=50` (max 100) returning `{ items, totalCount, page, pageSize }`.
- Enum query parameters are parsed **case-insensitively**: `?entityType=customer` works. `status` accepts a
  comma-separated list.
- Errors are RFC 7807 `ProblemDetails` with a stable `code` field. Messages are English — they are
  developer-facing, and the web app translates its own text.
- `409 Conflict` means "the rule refused this", and the `detail` says which rule. That is how a rejected status
  transition, or a completion blocked by an unchecked checklist item, comes back.
- Timestamps are ISO 8601 with an offset. Every period boundary and due date is computed in the tenant's
  configured IANA time zone, so `2026-07-27T23:59:59-05:00` is what a "Monday" due date looks like.

---

## Compatibility policy

`/api/v1` is stable. Concretely — this is what "we will not break it" means:

**We may:**

- add an endpoint;
- add an **optional** request field or query parameter;
- add a response field;
- **append** a member to an enum.

**We will not:**

- remove or rename a field, endpoint or parameter;
- change a field's type or a response's shape;
- change the status code returned for an existing condition;
- add a **required** request field;
- reorder or renumber an enum.

A change that does not fit the first list means `/api/v2`.

Two consequences worth planning for, because "additive" is not the same as "nothing changes":

1. **Handle unknown enum members.** New statuses and event types have been appended before and will be again.
2. **Ignore unknown response fields** rather than failing on them.

The enforcement mechanism is `openapi/v1.json`: every contract change shows up as a reviewable diff in a committed
file, and CI fails if the file and the code disagree.

---

## Webhooks

Everdue **posts out**; there is no inbound endpoint and nothing to open in a firewall. That is the same decision
behind Telegram long-polling: a self-hosted install behind NAT has to work.

### Events

| `type` | When |
|---|---|
| `workitem.created` | A one-off task is created, or the engine spawns an occurrence |
| `workitem.completed` | Completed — including late, flagged `"late": true` |
| `workitem.missed` | A period ended uncompleted |
| `workitem.onhold` | Put on hold |
| `workitem.reassigned` | The owner changed |
| `entity.created` | A customer, supplier, machine… was added |
| `ping` | Only from the admin **Send a test** button |

`completed` covers a late completion rather than splitting into two events: anybody who wants completions would
otherwise have to subscribe twice.

**After downtime, announcements are suppressed, not the records.** A catch-up tick that records two weeks of
misses sends webhooks only for the misses whose period ended in the last 24 hours, and no `created` events for
occurrences inserted already-concluded. The ledger still contains every row.

### Payload

Id plus minimal fields. A subscriber that needs more calls `GET /api/v1/workitems/{id}` with an API key — which is
why the two features ship together, and why the payload is a small thing to have promised forever.

```json
{
  "id": "0199c3e1-...",
  "type": "workitem.missed",
  "timestamp": "2026-07-29T12:00:00+00:00",
  "data": {
    "workItemId": "0199c3e0-...",
    "responsibilityId": "0199a1b2-...",
    "title": "Weekly inspection – Line 2",
    "status": "Missed",
    "dueDate": "2026-07-27T23:59:59-05:00",
    "periodStart": "2026-07-27T00:00:00-05:00",
    "periodEnd": "2026-08-03T00:00:00-05:00",
    "ownerUserId": "0199aaaa-...",
    "entityId": "0199bbbb-...",
    "entityName": "Acme Ltd",
    "departmentId": "0199cccc-...",
    "holdReason": null,
    "late": false
  }
}
```

No description, no comments, no attachments, no checklist, and **no custom fields** — those are display-only
reference values and never leave the entity screen.

### Verifying a delivery

Everdue signs to the [Standard Webhooks](https://www.standardwebhooks.com) specification, so an off-the-shelf
library can verify it:

```
webhook-id:        0199c3e1-...              the event id
webhook-timestamp: 1785110400                unix seconds
webhook-signature: v1,Base64(HMAC-SHA256(secret, "{id}.{timestamp}.{body}"))
```

The secret is shown once when the subscription is created.

```python
import base64, hashlib, hmac, time

def verify(secret: str, headers, body: str) -> bool:
    event_id = headers["webhook-id"]
    timestamp = headers["webhook-timestamp"]

    # Reject anything older than five minutes — this is what makes a captured request unreplayable.
    if abs(time.time() - int(timestamp)) > 300:
        return False

    expected = base64.b64encode(
        hmac.new(secret.encode(), f"{event_id}.{timestamp}.{body}".encode(), hashlib.sha256).digest()
    ).decode()

    # The header may carry several space-separated signatures during a secret rotation.
    return any(
        hmac.compare_digest(expected, part.split(",", 1)[1])
        for part in headers["webhook-signature"].split(" ")
        if part.startswith("v1,")
    )
```

Sign over the **raw body bytes**, before any JSON parsing or re-serialisation.

### Delivery guarantees

**At-least-once, not exactly-once** — and pretending otherwise would be a lie: a `200` that times out on the way
back is indistinguishable from a failure.

- `webhook-id` is **stable across retries**. Use it as your idempotency key.
- Retryable failures (`408`, `429`, `5xx`, timeouts, refused connections) back off exponentially, capped at an
  hour, up to `Webhooks:MaxAttempts` (default 5).
- Any other `4xx` fails immediately. Retrying a 404 for an hour is how an outbox becomes log spam.
- After `Webhooks:MaxConsecutiveFailures` failures in a row (default 10) the subscription is **disabled** and
  stays disabled until an administrator re-enables it. An endpoint that has failed ten times running has changed.
- Redirects are **not** followed. Your response body is read only far enough to quote in an error message, and is
  never treated as input.
- Answer quickly — the timeout is `Webhooks:TimeoutSeconds` (default 10). Queue the work; don't do it inline.

### URLs

`https` is required unless `Webhooks:AllowInsecureUrls` is set, which exists for a localhost receiver during
development.

**Private and loopback addresses are allowed.** That is a deliberate departure from the usual SSRF advice, and the
right one here: posting to an automation box on the same LAN is the self-hosted use case, only an administrator can
create a subscription, and that administrator can already read everything the payload would carry.

---

## Exporting

`GET /api/v1/exports/...` returns `text/csv` — UTF-8 **with a BOM**, so Excel reads accented text correctly, and
with the OWASP formula-injection guard applied to every cell.

| Route | Filters |
|---|---|
| `/exports/workitems` | the whole `/workitems` filter set |
| `/exports/reports/{entity-health\|neglect\|blocked-by-entity}` | the report's filters |
| `/exports/insights/{compliance\|reliability\|concentration\|hold-aging}` | the insight's scope and window |
| `/exports/raw/{entities\|responsibilities\|workitems\|workitem-events\|comments\|checklist-items}` | none |

Each export dispatches the same query its screen dispatches, so the file matches the screen by construction.

**No silent truncation.** A report or insight export over `Exports:MaxRows` (default 50 000) returns **400** with
an instruction to narrow the filters, never a shorter file. Raw table dumps are streamed and uncapped — they
contain no aggregation that could be wrong.

An export never widens what its source allows: `/exports/workitems` is open to any signed-in caller, the rest are
administrator-only.
