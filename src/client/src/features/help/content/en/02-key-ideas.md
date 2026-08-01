# How Everdue thinks

Six ideas. Once these make sense, every screen in the product makes sense.

## 1. A responsibility never finishes

A **responsibility** is an obligation that comes back: *"call Acme every Monday"*, *"inspect line 2 on
the 1st of each month"*, *"count the till every day"*.

You never complete a responsibility. It just keeps producing work.

## 2. Each period produces one occurrence

Every time the responsibility comes due, Everdue creates one **occurrence** — an actual piece of work
with a due date, on somebody's board.

*"Call Acme every Monday"* produces one occurrence each Monday. This week's call is a different piece
of work from last week's, and each is judged on its own.

Everdue creates occurrences automatically. Nobody has to remember, and nobody can forget.

## 3. A period that ends uncompleted is **missed** — and the next one comes anyway

This is the heart of the product.

If Monday's call did not happen by the time the next Monday starts, Monday's occurrence is recorded as
**missed**. It stays missed forever. And the new Monday appears regardless, so the work does not pile
up into one impossible item.

You can still complete a missed occurrence — it lands as **completed late**. The record then says both
things: it was missed, and it was eventually done. Neither erases the other.

> Most tools do the opposite: they move the date forward, and the miss disappears. Which is why they
> can never tell you what really happened last quarter.

## 4. A one-off task is different

A **one-off task** is work that happens once: *"send the new price list to Acme"*.

| | Occurrence | One-off task |
|---|---|---|
| Comes from | a responsibility | somebody creating it |
| Repeats | yes, every period | no |
| Can be missed | **yes** | no — it has no period to end |
| Can be cancelled | no (pause the responsibility instead) | yes |
| Counts in the compliance reports | yes | no |

Anyone can create a one-off task. Only an administrator can create a responsibility.

## 5. Work can be *about* something, and done *by* somebody

Two different fields, and people mix them up on day one:

- **Entity** — what the work is *about*: a customer, a supplier, a machine, a department, a company.
  It is a label, not a file: Everdue holds the *name*, not their contracts or invoices.
- **Department** — which team *executes* the work.

Both are optional. Filling them in is what makes the reports able to answer "how are we doing with
Acme?" and "how loaded is Operations?".

## 6. Status is a small, closed list

| Status | Means |
|---|---|
| **To do / Open** | Nobody has started it |
| **In progress** | Somebody has picked it up. A coordination signal only — it changes no report and does not protect anything from being missed |
| **On hold** | Parked, with a required reason |
| **Missed** | The period ended without it being completed. Only Everdue itself sets this |
| **Completed** | Done, inside its period |
| **Completed late** | Done, after its period ended |
| **Cancelled** | A one-off task that no longer applies |

**Overdue** is not in the list on purpose: it is not a status but a fact — anything not finished and
past its due date shows a red *Overdue* badge, whatever state it is in.

## Putting it together

> *"Inspect the generator on the 1st of every month"* is a **responsibility**, owned by Marta, about
> the **entity** *Generator #3*, executed by the **department** *Maintenance*.
>
> On 1 March, Everdue creates March's **occurrence**, due 1 March. Marta puts it **on hold** on the 2nd
> — *waiting on supplier*, the part has not arrived. The part is late; on 1 April the period ends and
> March is recorded **missed**. April's occurrence appears the same morning.
>
> The part arrives on 3 April. Marta completes March's occurrence: it becomes **completed late**. The
> March miss stays on the record, and the hold-aging report shows exactly how many days were spent
> waiting on that supplier.

Every number in Everdue is built from stories like that one. Nothing else needs to be typed in for the
reports to work.
