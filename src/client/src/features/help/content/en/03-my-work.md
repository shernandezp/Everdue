# My work — the board

The board is the screen you will use most. It shows work as cards in five columns, and moving a card
is how you record what happened.

## The five columns

| Column | What belongs there |
|---|---|
| **To do** | Not started |
| **In progress** | Somebody has picked it up |
| **On hold** | Parked, waiting on somebody or something. A reason is required |
| **Missed** | The period ended uncompleted. Only Everdue puts cards here |
| **Done** | Completed in the last 7 days |

The number beside each column title is how many cards are in it.

## Reading a card

| On the card | Meaning |
|---|---|
| Title | What has to be done |
| Grey line under it | The entity the work is about, if any |
| Coloured badge | The status |
| Red **Overdue** badge | Past its due date and not finished |
| Orange badge | Why it is on hold |
| **3/7** badge | Checklist progress: three of seven steps ticked |
| Date on the right | When it is due — red if that date has passed |

Click anywhere on a card to open the full detail panel. See
[Working on an item](05-work-item).

## Moving work

**Drag the card** to another column, or use the **⋮** menu in its corner. The menu is easier on a
phone, and it is the same set of actions.

| Move | What happens |
|---|---|
| To do → In progress | Marks it as being worked on |
| To do → Done | Completes it. You do not have to pass through *In progress* |
| In progress → Done | Completes it |
| Anything → On hold | Asks for a reason first |
| On hold → To do / In progress | Releases the hold |
| Missed → Done | Completes it **late**. The miss stays on the record |
| Done → To do | Undoes a completion. Only the owner or an administrator can do this |

Some moves are refused, and Everdue tells you so:

- **Nothing can be dragged into *Missed*.** Only Everdue records a miss, when a period ends.
- **A missed card cannot go to *In progress*.** It stays visible as missed until it is completed late.
  Otherwise it would quietly disappear from the reports while somebody worked on it.
- **An occurrence cannot be cancelled.** Cancelling is for one-off tasks. To stop a recurring
  obligation, an administrator pauses or deactivates the responsibility.

## Putting something on hold

Choose the reason that is true. It is a short list on purpose, so that the report built from it is
worth reading:

| Reason | Use it when |
|---|---|
| Waiting on customer | You need something from the customer |
| Waiting on supplier | You need something from a supplier |
| Waiting for approval | Somebody inside the organisation has to approve |
| Missing information | You do not have what you need to do the work |
| Other | Anything else — a written explanation is then **required** |

Two things worth knowing:

- **A hold never stops a miss.** If the period ends while the work is on hold, it is still recorded as
  missed. A hold explains a delay; it does not excuse the period.
- Everdue measures how long each hold lasted. That is what turns *"we are always waiting on that
  supplier"* into a number your manager can act on.

## Seeing somebody else's work

At the top of the board there is a **Showing** picker:

- **Your own name** (the default) — your work.
- **Empty** — everybody's work.
- **A colleague** — what that person is doing and what is queued for them.

This is not surveillance; it is how you cover for somebody who is off sick. Anyone may work on anyone's
item, and every change is recorded with a name against it.

## New task

**New task** creates a one-off piece of work:

1. **Title** — what has to be done. Be specific: *"Send Acme the updated price list"* beats *"Acme"*.
2. **Owner** — who is responsible. It defaults to you.
3. **Entity** and **Department** — optional, but they are what make the reports useful later.
4. **Due date** — the task is due at the end of that day.

To create work that repeats, you need a **responsibility** instead — see
[Responsibilities](07-responsibilities).

## If the board is empty

Either everything is finished — genuinely possible on a good Friday — or no work has been assigned to
you yet. Clear the **Showing** picker to see whether the team has any work at all.
