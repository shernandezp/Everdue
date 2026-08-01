import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

/**
 * Locales are discovered, not listed.
 *
 * Adding a language to the client is therefore <strong>one JSON file</strong> and no change to any `.ts` or `.tsx`
 * file at all — which is the point: a translation is data. `import.meta.glob` is resolved by Vite at build time,
 * so the files are still bundled statically.
 *
 * The *authoritative* list is the server's `Languages.Supported`, exposed by `GET /api/v1/languages`; a JSON file
 * present here but absent from that list is simply not offered. See `docs/translating.md` for the four files a
 * language touches — one of them fails silently if it is missed, which is why there is a server test for it.
 */
const modules = import.meta.glob<{ default: Record<string, unknown> }>('./locales/*.json', { eager: true });

const resources = Object.fromEntries(
  Object.entries(modules).map(([path, module]) => [
    path.replace('./locales/', '').replace('.json', ''),
    { translation: module.default },
  ]),
);

/** What this bundle can render. The server decides what may be *chosen*. */
export const BUNDLED_LANGUAGES = Object.keys(resources);

/** Spanish is the product's default, and the fallback when a preference names something absent. */
export const DEFAULT_LANGUAGE = 'es';

void i18n.use(initReactI18next).init({
  resources,
  lng: DEFAULT_LANGUAGE,
  fallbackLng: DEFAULT_LANGUAGE,
  supportedLngs: BUNDLED_LANGUAGES,
  interpolation: { escapeValue: false },
  returnNull: false,
});

/**
 * The language is the user's preference, falling back to the tenant default. It is applied once the session is
 * known rather than guessed from the browser, so a shared machine shows each person their own language.
 */
export function applyLanguage(language: string | null | undefined): void {
  const next = language && BUNDLED_LANGUAGES.includes(language) ? language : DEFAULT_LANGUAGE;

  if (i18n.language !== next) {
    void i18n.changeLanguage(next);
  }

  document.documentElement.lang = next;
}

export default i18n;
