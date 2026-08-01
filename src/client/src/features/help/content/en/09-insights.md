# Insights

*Administrators only.*

Reports answer *"what needs attention today?"*. Insights answer **"what keeps happening?"** — the same
records, read over months instead of hours.

Nothing extra is typed in to produce any of it. Turn Everdue on today and the insights work
retroactively on whatever history you already have.

## The window

Every insight screen has a **Group by** (week or month) and a **Window** (how many periods back). The
newest column is always the period still in progress, so it looks low — the screen says so under the
chart.

## Compliance — per responsibility

**On-time completions ÷ periods that have ended.**

| Column | Means |
|---|---|
| **Compliance** | The percentage, with the pair it came from beside it — *87% · 26/30* |
| **On time** | Completed inside their period |
| **Late** | Completed after the period ended — these count as misses |
| **Missed** | Never completed |
| **In progress** | Periods not finished yet; they are not in the percentage |
| **Trend** | The shape of the last N periods |

Two rules that keep the number honest:

- **A late completion counts as a miss.** It happened, and it happened late; the percentage says the
  second thing.
- **Under five finished periods, no percentage is shown at all** — just the counts. 95% of 200 is not
  100% of 3, and a young responsibility should not look perfect.

Click a responsibility to see its own page: the same figures, a line over time, and the strip of
individual periods — ✅ week 29, ❌ week 30, ⏸ week 31. Click any period to open that occurrence.

## Reliability — per person

The same arithmetic, per person, over **recurring work only**. A one-off task can never be missed, so
counting it would flatter everybody equally; one-off completions get their own column instead.

This screen is built to be read as *where do I help?*, and its design says so:

- **Administrators only.** Nobody sees a colleague's numbers, and nobody is notified about their own.
- **No ranking, no leaderboard, no badges, no targets** — and there will not be.
- **A percentage never appears without its volume**, and thin denominators are withheld entirely.
- **External waits count in the percentage**, and are shown beside it. Removing them would let anyone
  improve their own number by parking work on hold, which is the opposite of what the hold reasons are
  for. What "waiting on the customer" cost is visible in the same row as the miss it explains.
- Numbers follow the **current owner** of each item, and the screen shows how many things changed hands
  in the window — which is often the real explanation.

## Completed work by entity

How much finished work each customer, supplier or machine accounts for, period by period, with a
stacked chart.

Two honest limits, stated on the screen:

- It is a **count of work items, not hours**. Everdue holds no time data: a two-minute call and a
  full-day inspection each count as one. It measures *attention*, not effort.
- Work nobody linked to an entity is reported as its own total rather than being quietly left out.

Only the top entities are charted; the rest are counted and reported as *"N more not shown"*.

## Hold aging

Where the waiting goes: total days, average days and the longest single wait, per reason and per
entity, with how many holds are still open.

- The figures are **calendar days** — nights and weekends included. Business hours would need a shift
  and holiday calendar, which Everdue does not have and is not going to grow.
- The history is rebuilt from the record of what happened, so this works for months before anybody
  thought to measure it.

*"Waiting on supplier: 214 days across 31 holds, longest 22"* is a supplier conversation with a number
attached.

## Chronically delayed

Also on the exceptions dashboard: responsibilities that missed **K of their last N finished periods**,
by default three of eight. One bad week is life; three in eight is a design problem — the wrong owner,
the wrong day, or an obligation nobody ever agreed to.

## Exporting

Every insight table exports to CSV with the same window and filters you are looking at.
