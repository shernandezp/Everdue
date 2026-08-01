# Responsibilities

*Administrators only.*

A **responsibility** is a permanent obligation: the work that comes back every day, week, month or
year. You set it up once, and Everdue produces the actual work from then on.

## Creating one

**Responsibilities → New responsibility.**

| Field | What to put |
|---|---|
| **Title** | What has to happen, in the words the team uses: *"Weekly follow-up with Acme"* |
| **Description** | Optional. Anything the person doing it needs to know |
| **Owner** | Who is responsible. Every occurrence starts on their board |
| **Entity** | What it is about — the customer, the machine. Optional but valuable |
| **Department** | Which team executes it. Optional |
| **Repeats** | Daily, weekly on chosen days, monthly on a day, or yearly |
| **Starts on** | The first date it applies from — **read the warning below** |

### How each repeat works

| Repeats | You choose | Example |
|---|---|---|
| **Every day** | nothing | The daily till count |
| **Weekly on selected days** | one or more weekdays | Mon + Thu deliveries |
| **Monthly on a day** | a day number, 1–31 | Invoicing on the 5th |
| **Yearly** | a month and a day | The insurance renewal |

A day that does not exist in a short month falls on that month's last day: *the 31st* is 28 February
in a normal year, and 29 February in a leap year.

## ⚠ The start date decides how much history you create

If you set **Starts on** to a date in the past, Everdue fills in every period between then and now —
and every one of those periods has already ended, so **every one is recorded as missed**.

Those misses are real as far as the reports are concerned, because a miss in the record is a miss.
That is the guarantee the whole product rests on, and it is not softened for back-dated work.

**Set the start date to today** unless you deliberately want that history — for instance because you
are migrating from a spreadsheet and the misses genuinely happened.

## What happens next

From the start date, Everdue creates one occurrence per period, on the owner's board, due at the end of
the scheduled day. It does that whether or not the previous one was completed — the work does not pile
up into one impossible item.

When a period ends without a completion, that occurrence is recorded **missed** and the next one
appears anyway.

## Checklist template

A responsibility can carry an ordered list of steps.

1. Add steps in the order they should be done.
2. Tick **Required** on the ones that genuinely must happen.
3. Use the arrows to reorder, the bin to remove.

Every occurrence gets **its own copy** of the template at the moment it is created. Improving the
template changes future occurrences only — history is never rewritten, which is what makes an old
inspection still show what was actually asked at the time.

## Completion rules

Two optional rules, both enforced by the server rather than merely suggested by the screen:

| Rule | Effect |
|---|---|
| **Require the checklist before completing** | Every *required* step must be ticked first |
| **Require a photo or file before completing** | At least one attachment must exist first |

Both apply **from the next completion onwards**. Nothing already completed is reopened, and an open
occurrence is not blocked until somebody actually tries to finish it.

Use them where they earn their keep — a safety inspection, a delivery that needs photographic proof —
and not everywhere, or people learn to tick without looking.

## Pausing

**Pause** stops new occurrences through a date you choose. Periods that fall entirely inside the pause
are skipped when it resumes: a sanctioned pause is **not** a miss.

Use it for a factory shutdown, a customer on holiday, a machine out of service. Use **Resume** to end
it early.

## Handing a responsibility over

**Hand over** changes the owner. Future occurrences follow the new owner automatically. You choose
whether the work already on the old owner's plate moves too — usually yes when somebody leaves, often
no when somebody is away for a week.

Every change of owner is recorded on the affected items, and the exception dashboard counts how many
things changed hands in the period.

## Deactivating

The bin icon deactivates a responsibility: no new occurrences, and existing ones stay exactly as they
are in the record. Nothing is deleted, and nothing that already happened changes.

Deactivate when the obligation genuinely ends. Pause when it will come back.

## Reading the list

| Column | Notes |
|---|---|
| Title | With badges: paused, how many checklist steps, whether the completion rules are on |
| Repeats | The rule in words, e.g. *Weekly · Mon, Thu* |
| Owner · Entity | Who and what it is about |
| Next occurrence | When the next one will appear |
| Status | Active or inactive |
