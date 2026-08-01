# Questions and problems

## About the work itself

**Something is marked "missed" but we did it.**
Complete it anyway: it becomes *completed late*, and the record then shows both facts. The miss is not
removed, on purpose — that is what makes a report months from now trustworthy.

**Can I delete a miss?**
No. Nothing in Everdue deletes a miss, and no setting turns that off. If it was recorded because a
responsibility was created with a past start date, deactivate that responsibility and create a new one
starting today.

**A pile of missed items appeared this morning.**
Somebody created a responsibility with a **start date in the past**. Everdue filled in every period
between that date and now, and every one of them had already ended. See
[Responsibilities](07-responsibilities).

**Why can I not drag a missed card back to "In progress"?**
Because it would leave the missed counts while somebody worked on it, and the numbers would quietly
stop being true. A missed item stays missed until it is completed late.

**Why can I not cancel an occurrence?**
Cancelling is for one-off tasks. An occurrence is one period of an ongoing obligation — if the
obligation has stopped, pause or deactivate the responsibility; if only this period does not apply,
put it on hold with a reason.

**I put it on hold, so it should not count as missed.**
It still does. A hold explains a delay; it does not stop the period ending. What a hold gives you is
the measurement: the hold-aging report shows exactly how long you waited and on whom.

**Someone else changed my item.**
That is allowed by design — in a small team people cover for each other. Open the item's **History**:
every change names who made it and what it was before.

**Can I edit a comment?**
No. Comments are a record, not a document. Delete your own and write a new one.

## Dates and time

**Why is the due date 23:59?**
An occurrence is due at the end of its day. The period itself ends at midnight when the next
occurrence starts.

**Which time zone is used?**
Your organisation's, set by an administrator. Every due date and period boundary is computed in it, so
the boundaries do not move when clocks change.

**Monthly on the 31st — what happens in February?**
It falls on the last day of that month: 28 February, or 29 in a leap year.

**Overdue and missed are not the same?**
Correct. *Overdue* means past the due date and still open — there is still time. *Missed* means the
period ended without it being completed, and that is permanent.

## Screens and access

**I cannot see Reports / Insights / Administration.**
Those are for administrators. Ask yours if you need access.

**A number on a report looks wrong.**
Click it. Every number opens the exact list of work behind it — the rows are the explanation.

**A percentage shows a dash instead of a number.**
Fewer than five finished periods. Everdue will not show a percentage that thin; the counts beside it
are the honest version.

**The newest column of a chart is always low.**
It is the period still in progress. Every insight screen says so under the chart.

## Alerts

**I am not getting messages.**
Check **Profile → Notifications**: a channel chosen, and the alert types you want switched on. If no
channel is offered, your organisation has not configured one yet. See
[Alerts and messages](10-notifications).

**I got a message for something that happened weeks ago.**
Unlikely: after an outage Everdue records every miss but announces only the recent ones, so nobody
wakes up to two hundred messages.

**Can I get a daily summary instead of individual alerts?**
Administrators can, under **Profile → Manager digest**: one e-mail with what was missed, what is due
and what is blocked.

## Your account

**How do I change my password?**
Profile → your name, top right. It needs 10 characters with upper case, lower case and a digit, and it
cannot be the same as the old one.

**I forgot my password.**
Ask an administrator to reset it. There is no self-service reset, deliberately.

**How do I change the language?**
**Profile → Preferred language.** Everything changes, including the messages Everdue sends you.

**Dark mode?**
Top-right menu → **Appearance**: light, dark, or follow your system. The choice is remembered on this
device.

## Data

**Can I get my data out?**
Yes. Every list and report has **Export CSV**, and the file matches the screen exactly. Administrators
can also export raw tables under the API section.

**Can I import a customer list?**
Yes — **Administration → Import**, or the link on the empty entities screen. An import creates or
skips; it never overwrites.

**Is anything ever deleted?**
Almost nothing. Entities, departments, responsibilities and users are *deactivated*, keeping their
history. Comments and attachments can be deleted by their author or an administrator. Work items and
their history are never deleted.

## Still stuck

Your administrator can see delivery health, user access and every setting on this installation. If
something looks like a bug in Everdue itself, they know where to report it.
