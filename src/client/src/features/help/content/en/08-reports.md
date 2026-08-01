# Reports

*Administrators only.*

Reports answer **"what needs attention today?"**. They are built entirely from the work itself — nobody
fills anything in for a report to exist.

Every number is a link. Click it and you land on the list, filtered to exactly the work behind that
figure. If a number looks wrong, click it and look at the rows.

## The filter bar

Every report shares three filters: **owner**, **department** and **entity type**. Set them once at the
top and the whole screen narrows.

## Exceptions — the daily screen

Five tiles across the top:

| Tile | Means |
|---|---|
| **Due today** | Due before the end of today and not finished |
| **Completed today** | Finished today — the only "good news" number on the screen |
| **Overdue** | Past the due date, not finished, period not over yet |
| **Missed** | Periods that ended uncompleted, in the range |
| **On hold** | Parked right now |

Below them:

- **On hold by reason** — where the waiting is, with the oldest hold in each group. A large *waiting on
  supplier* group with a hold three weeks old is a conversation, not a statistic.
- **Chronically delayed** — responsibilities that keep being missed, not the ones missed once. By
  default: three misses in the last eight finished periods.
- **Items reassigned in period** — how much work changed hands. A high number often explains a bad
  compliance figure better than any individual does.

## Entity health

One row per customer, supplier or machine:

| Column | Means |
|---|---|
| **Open** | Work outstanding right now |
| **Overdue** | Of those, how many are past their date |
| **Missed 30d / 60d / 90d** | Periods missed in the last 30, 60 and 90 days |
| **On hold** | Parked right now |
| **Last activity** | The last *completed* work — nothing else counts |
| **Days since** | How long ago that was |

Read the three missed columns together: 5 / 5 / 5 is an old problem that has stopped; 5 / 2 / 1 is a
problem that is getting worse.

## Neglect

Entities with **no completed work** for longer than N days — 90 by default; change the number at the
top.

"Last activity" means the last completed piece of work and nothing else. No opened e-mail, no logged
call, no automatic touch. That is what makes this list trustworthy where a CRM's activity log is not.

An entity that has never had any completed work shows **∞** rather than a number: it has not been
waiting a certain number of days, it has been waiting the whole time.

## Blocked by entity

All the work currently on hold, grouped by entity and reason, with the oldest hold in each group.

Use it before a call: *"we have four things waiting on you, the oldest since the 3rd"* is a much better
opening than *"we are still waiting"*.

## Entity timeline

Reached by clicking an entity anywhere in the product. Every occurrence and one-off task about it,
newest first, with status, dates and checklist progress. The whole relationship, in order.

## Exporting

Entity health, neglect and blocked-by-entity each have an **Export CSV** button. The file contains
exactly the rows on screen with the filters you set. Over 50,000 rows Everdue refuses and asks you to
narrow the filters rather than handing you an incomplete file.
