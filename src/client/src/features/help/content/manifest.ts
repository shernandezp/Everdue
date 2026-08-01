import {
  IconAdjustments,
  IconBell,
  IconBuildingStore,
  IconChartHistogram,
  IconClipboardList,
  IconLayoutKanban,
  IconList,
  IconQuestionMark,
  IconReportSearch,
  IconRepeat,
  IconRocket,
  IconSchool,
  type TablerIcon,
} from '@tabler/icons-react';

export type HelpTopic = {
  /** Also the file name in every language folder, and the URL: /help/{slug}. */
  slug: string;
  icon: TablerIcon;
  title: Record<HelpLanguage, string>;
};

export type HelpLanguage = 'en' | 'es';

/**
 * The manual's table of contents.
 *
 * Titles live here rather than in the locale files because they belong to the article, not to the
 * interface: adding a topic is two markdown files and one entry, with no i18n key to remember. Every
 * topic must exist in every language — a manual that is complete in one language and not the other is
 * a broken help page for half the users.
 */
export const HELP_TOPICS: HelpTopic[] = [
  {
    slug: '01-getting-started',
    icon: IconRocket,
    title: { en: 'Getting started', es: 'Primeros pasos' },
  },
  {
    slug: '02-key-ideas',
    icon: IconSchool,
    title: { en: 'How Everdue thinks', es: 'Cómo piensa Everdue' },
  },
  {
    slug: '03-my-work',
    icon: IconLayoutKanban,
    title: { en: 'My work — the board', es: 'Mi trabajo — el tablero' },
  },
  {
    slug: '04-list-and-views',
    icon: IconList,
    title: { en: 'The list and saved views', es: 'La lista y las vistas guardadas' },
  },
  {
    slug: '05-work-item',
    icon: IconClipboardList,
    title: { en: 'Working on an item', es: 'Trabajar en un elemento' },
  },
  {
    slug: '06-entities',
    icon: IconBuildingStore,
    title: { en: 'Entities', es: 'Entidades' },
  },
  {
    slug: '07-responsibilities',
    icon: IconRepeat,
    title: { en: 'Responsibilities', es: 'Responsabilidades' },
  },
  {
    slug: '08-reports',
    icon: IconReportSearch,
    title: { en: 'Reports', es: 'Reportes' },
  },
  {
    slug: '09-insights',
    icon: IconChartHistogram,
    title: { en: 'Insights', es: 'Análisis' },
  },
  {
    slug: '10-notifications',
    icon: IconBell,
    title: { en: 'Alerts and messages', es: 'Avisos y mensajes' },
  },
  {
    slug: '11-administration',
    icon: IconAdjustments,
    title: { en: 'Administration', es: 'Administración' },
  },
  {
    slug: '12-faq',
    icon: IconQuestionMark,
    title: { en: 'Questions and problems', es: 'Preguntas y problemas' },
  },
];

/**
 * Every article, as a loader. Lazy on purpose: the manual is ~90 kB of prose per language and belongs
 * nowhere near the bundle somebody downloads to look at their board.
 */
const ARTICLES = import.meta.glob('./*/*.md', { query: '?raw', import: 'default' }) as Record<
  string,
  () => Promise<string>
>;

/** The article's markdown, or null when the file is missing (a topic added to the manifest only). */
export async function loadArticle(language: HelpLanguage, slug: string): Promise<string | null> {
  const loader = ARTICLES[`./${language}/${slug}.md`];
  return loader ? await loader() : null;
}

/** The two languages the manual exists in, mapped from whatever the interface is currently set to. */
export function helpLanguage(uiLanguage: string | undefined): HelpLanguage {
  return uiLanguage?.toLowerCase().startsWith('en') ? 'en' : 'es';
}
