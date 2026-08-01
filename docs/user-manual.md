# The user manual

The manual is **inside the product**: sign in and open **Help**, or go to `/help`. It renders in the
same language as the rest of the app, so a Spanish-speaking user reads Spanish screenshots' worth of
instructions without switching anything.

## Where it lives, and why there

```
src/client/src/features/help/
  content/manifest.ts      the topic list: slug, icon, and a title per language
  content/en/*.md          the manual, English
  content/es/*.md          the manual, Spanish
  markdown.tsx             a small markdown → React renderer
  HelpPage.tsx             the screen
```

It lives with the client because the app **ships** it — there is no second copy to drift, no network
fetch, and an install with no internet access still has its documentation. Articles are imported
lazily, one per topic, so the manual is not in the initial bundle.

## Editing it

Edit the markdown. That is the whole workflow — no build step, no export, no CMS. Two rules:

1. **Both languages, in the same change.** A topic that exists in one language and not the other is a
   broken Help page for half the users. `manifest.ts` lists every topic once with a title per language,
   so a missing file is a compile-time-visible gap rather than a surprise at runtime.
2. **Use the words the interface uses.** The manual says *owner*, *occurrence*, *on hold*, *missed*;
   in Spanish, *responsable*, *ocurrencia*, *en espera*, *incumplida* — exactly as the buttons do.
   Take the vocabulary from `src/client/src/i18n/locales/{en,es}.json`, never from a dictionary.

## Adding a topic

1. Write `content/en/<nn>-<slug>.md` and `content/es/<nn>-<slug>.md`. Start each with a single `# H1`.
2. Add an entry to `content/manifest.ts`: the slug, an icon, and the title in both languages.
3. That is it — the topic appears in the sidebar, in order, in both languages.

## What the renderer supports

Headings (`#`–`####`), paragraphs, **bold**, *italic*, `code`, links, bullet and numbered lists,
blockquotes, fenced code blocks, tables, and horizontal rules. Deliberately no raw HTML: the renderer
produces React elements rather than injecting markup, which is what keeps the Content-Security-Policy
posture unchanged.

## Who it is for

Somebody who has never seen Everdue and does not know what a "responsibility" is. It explains what
each screen is for, what every option does, and shows worked examples. It is not the API reference
([api.md](api.md)) and not an operations runbook ([operations.md](operations.md)).
