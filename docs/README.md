# Everdue documentation

Three audiences, three sets of documents.

## For people using Everdue

The **user manual** is inside the app: sign in and open **Help** (`/help`), in English or Spanish —
whichever language the account is set to. It is a step-by-step guide for somebody who has never seen
the product: what each screen is for, what every option does, and worked examples.

Its source is markdown, one file per topic per language, at
[`src/client/src/features/help/content/`](../src/client/src/features/help/content). It lives with the
client because the app ships it; edit those files and the in-app manual changes.
See [user-manual.md](user-manual.md) for how to add or translate a topic.

## For people running an installation

| | |
|---|---|
| [operations.md](operations.md) | Install, upgrade, back up, restore, monitor, and what to do when something looks wrong |
| [configuration.md](configuration.md) | Every setting, its default and what it changes |
| [../README.md](../README.md) | What Everdue is, the quickest install, and what each version added |

## For people working on Everdue

| | |
|---|---|
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | How to run it, the checks CI runs, the discipline the codebase expects |
| [architecture.md](architecture.md) | Layers, the ledger, the engine, key decisions and why they are what they are |
| [testing.md](testing.md) | Three ways to run it locally, a five-minute walkthrough, and the test suites |
| [api.md](api.md) | The public API, authentication, the compatibility policy, webhooks, exports |
| [openapi/v1.json](openapi/v1.json) | The contract itself — generated at build time and diffed by CI |
| [translating.md](translating.md) | Adding a language. It is four data files, one of which fails silently if you miss it |
| [../SECURITY.md](../SECURITY.md) | Reporting a vulnerability, privately |

## The rule that shapes all of it

> **This system manages WORK, not business data.**

It *references* a customer; it does not store their contracts, invoices or pipeline. Every document
here assumes that boundary, and features that fail its litmus test — *does this help answer "what work
needs to happen, what happened, and what requires attention?"* — are declined however reasonable they
are individually.
