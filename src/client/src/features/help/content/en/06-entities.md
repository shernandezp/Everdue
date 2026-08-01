# Entities

An **entity** is what a piece of work is *about*: a customer, a supplier, a machine, a department or a
company.

## What an entity is — and is not

An entity is a **label**, not a file. Everdue stores its name, its type and whether it is still active,
plus a few display-only fields your administrator may have defined. It does **not** store contracts,
invoices, opportunities, contact histories or documents. That is a deliberate boundary: Everdue manages
work, and the moment it starts holding business data it becomes a bad version of a different product.

What you get in exchange for that restraint is that every report can be cut by entity without anybody
ever typing anything twice.

## The five types

| Type | Typically |
|---|---|
| **Customer** | Who you serve |
| **Supplier** | Who serves you |
| **Equipment** | A machine, a vehicle, an installation |
| **Department** | When the work is *about* a department rather than done by one |
| **Company** | A group company or a branch |

> **Entity of type Department** and the **Department** field are different things on purpose. The
> department field says *who executes the work*; an entity of type department says *the work is about
> that department* — an internal audit, for example.

## Creating one

1. **Entities → New entity.**
2. **Name** — how people actually say it. Two entities of the same type cannot share a name.
3. **Type** — from the five above.
4. Fill in any **custom fields** your administrator has defined for that type.
5. **Create**.

Members can read the entity list; administrators create and edit.

## Custom fields

An administrator can add up to ten extra fields per entity type — an account manager on a customer, a
serial number on a machine. Four kinds: text, number, date, or a list to choose from.

They are **display-only**. They do not filter, sort, appear in reports, or trigger anything. They exist
because that one extra column is often the last reason a team still keeps a spreadsheet.

## Deactivating an entity

The bin icon deactivates an entity; it never deletes it. Its history stays, every past piece of work
keeps pointing at it, and it stops appearing in pickers for new work. **Show inactive** brings them
back into the list.

## The entity timeline

Click an entity's name to open its **timeline**: every occurrence and one-off task about it, newest
first, with status, dates and checklist progress.

This is the customer-service memory. Before you call Acme back, open Acme: *"Week 29 done, week 30
missed, week 31 waiting on you"* is on one screen, in order.

## Importing a list you already have

If your customers are in a spreadsheet, do not type them in again — see
[Administration](11-administration#importing-from-a-spreadsheet). The empty state on this screen links
straight to the importer.

## Everyday examples

| You want to | Do this |
|---|---|
| See everything about one customer | Entities → click the name |
| Find customers nobody has touched for months | Reports → Neglect |
| Know which customers absorb the most work | Insights → Completed work |
| Record the machine a job was about | Set **Entity** on the work item |
| Record which team does the job | Set **Department** on the work item |
