# Working on an item

Clicking any card or row opens the detail panel. Everything you can do to one piece of work is here.

## The top of the panel

The title, the status badges, and whether this is an **occurrence** (part of something recurring) or a
**one-off task**. Under that, a card of facts: owner, entity, department, due date, the period an
occurrence belongs to, and who completed it, if anybody has.

## The action buttons

Only the actions that are legal right now are shown, so you can never reach a dead end.

| Button | What it does |
|---|---|
| **Complete** | Finishes the work. After the period has ended it says *Complete (late)* |
| **Start working** | Marks it as in progress. Optional — you can complete straight from *To do* |
| **Put on hold** | Parks it. A reason is required |
| **Reopen** | Releases a hold, or undoes an on-time completion (owner or administrator). A *completed late* item cannot be reopened — its period is closed and the miss stands |
| **Reschedule** | Moves the due date |
| **Edit** | Changes the title, description, owner, entity or department |
| **Cancel task** | One-off tasks only |

If **Complete** is greyed out, the reason is written right below the buttons — unticked required
steps, or a photo the responsibility demands.

## Checklist

Some work carries steps. A responsibility can define a **template**, and every occurrence gets its own
copy when it is created — so improving the template never rewrites what past occurrences looked like.

- Tick a step when you have done it. Everdue records **who** ticked it and **when**; hover the line to
  see that.
- **Add a step** puts an extra line on *this item only*. Extra lines are never mandatory.
- A line marked **Required** must be ticked before the item can be completed — but only when the
  responsibility says the checklist is enforced.
- You can delete a step you added; you cannot delete one that came from the template, because it is
  part of what the occurrence was asked to be.

## Attachments and proof

**Attach a file** uploads a document or photo. **Take a photo** opens the camera directly on a phone —
two taps, no app to install.

**Take a photo** only appears on a phone or tablet. A computer has no camera the browser can open this
way, so there it would be a second button doing what **Attach a file** already does — upload the photo
from your computer instead.

Some responsibilities **require** a photo or file before the work can be completed. When that is the
case the panel says so before you try, not after.

Whoever uploaded a file can delete it; so can an administrator.

## Comments and mentions

Comments are the story of the work: what you found, what the customer said, why it took two attempts.

1. Type in the box at the bottom.
2. To pull somebody in, use **Mention** and pick them from the list.
3. Press **Add a comment**.

Anybody mentioned gets an alert with a link straight to this item. Comments cannot be edited — they are
a record, not a document — but you can delete your own, and administrators can delete any.

## Rescheduling

**Reschedule** moves the due date and asks for an optional note explaining why. The note goes into the
history.

One rule: **an occurrence can only move inside its own period.** March's inspection can move from the
1st to the 6th of March; it cannot move to April, because April already has its own occurrence
waiting. A one-off task can move anywhere.

## History

The timeline at the bottom is the complete record of the item, newest last:

| Entry | Means |
|---|---|
| **Created** | It came into existence — by hand, or from a responsibility |
| **Status changed** | From one state to another, with both names |
| **Edited** | A field changed, and which fields |
| **Handed over** | The owner changed |
| **Rescheduled** | With the old and new dates |
| **Comment added** | Somebody wrote something |

Every entry names the person, or says **by the occurrence engine** when Everdue itself did it — which
is what a recorded miss looks like.

Ticking checklist steps deliberately does not appear here: fifteen ticks would bury the history the
timeline exists for. The steps themselves record who ticked them.

## Why anyone can edit anyone's work

In a small team, cover matters more than territory: if somebody is off sick, a colleague has to be
able to finish their work. So Everdue lets anyone edit or complete anyone's item, and instead makes
**every change traceable**. Two exceptions, because both erase a record rather than adding one:
undoing a completion, and cancelling a task, stay with the owner and administrators.
