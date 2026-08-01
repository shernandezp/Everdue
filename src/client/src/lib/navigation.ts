import {
  IconAlertTriangle,
  IconBell,
  IconBriefcase,
  IconBuildingStore,
  IconChartBar,
  IconChartHistogram,
  IconClipboardList,
  IconFileImport,
  IconHeartRateMonitor,
  IconHourglass,
  IconLayoutKanban,
  IconList,
  IconMoodEmpty,
  IconRepeat,
  IconReportSearch,
  IconSettings,
  IconSettings2,
  IconTrendingUp,
  IconUser,
  IconUserCheck,
  IconUsers,
  type TablerIcon,
} from '@tabler/icons-react';
import { routes } from './routes';
import { SECTION_COLOR, type SectionId } from '../theme';

export type NavItem = { to: string; labelKey: string; icon: TablerIcon };

export type NavSection = {
  id: SectionId;
  labelKey: string;
  icon: TablerIcon;
  /** Members see their own work and the entity screens; the rest is a manager's surface. */
  adminOnly: boolean;
  items: NavItem[];
};

/**
 * The navigation, as data.
 *
 * The navbar builds its accordion from this and every page header reads its icon and accent colour
 * back out of it, so a screen cannot end up wearing a different icon than the link that reached it.
 */
export const NAV_SECTIONS: NavSection[] = [
  {
    id: 'work',
    labelKey: 'nav.work',
    icon: IconBriefcase,
    adminOnly: false,
    items: [
      { to: routes.board, labelKey: 'nav.myWork', icon: IconLayoutKanban },
      { to: routes.work, labelKey: 'nav.list', icon: IconList },
      { to: routes.entities, labelKey: 'nav.entities', icon: IconBuildingStore },
    ],
  },
  {
    // Reports answer "what needs attention today".
    id: 'reports',
    labelKey: 'nav.reports',
    icon: IconReportSearch,
    adminOnly: true,
    items: [
      { to: routes.dashboard, labelKey: 'nav.dashboard', icon: IconAlertTriangle },
      { to: routes.entityHealth, labelKey: 'nav.entityHealth', icon: IconHeartRateMonitor },
      { to: routes.neglect, labelKey: 'nav.neglect', icon: IconMoodEmpty },
      { to: routes.blocked, labelKey: 'nav.blocked', icon: IconClipboardList },
    ],
  },
  {
    // Insights answer "what keeps happening".
    id: 'insights',
    labelKey: 'nav.insights',
    icon: IconTrendingUp,
    adminOnly: true,
    items: [
      { to: routes.compliance, labelKey: 'nav.compliance', icon: IconChartHistogram },
      { to: routes.reliability, labelKey: 'nav.reliability', icon: IconUserCheck },
      { to: routes.concentration, labelKey: 'nav.concentration', icon: IconChartBar },
      { to: routes.holdAging, labelKey: 'nav.holdAging', icon: IconHourglass },
    ],
  },
  {
    id: 'admin',
    labelKey: 'nav.admin',
    icon: IconSettings2,
    adminOnly: true,
    items: [
      { to: routes.responsibilities, labelKey: 'nav.responsibilities', icon: IconRepeat },
      { to: routes.departments, labelKey: 'nav.departments', icon: IconBuildingStore },
      { to: routes.users, labelKey: 'nav.users', icon: IconUsers },
      { to: routes.import, labelKey: 'nav.import', icon: IconFileImport },
      // The integrator surfaces — custom fields, API keys, webhooks — are tabs of Settings, not
      // separate destinations. Their old routes redirect there (see App.tsx).
      { to: routes.settings, labelKey: 'nav.settings', icon: IconSettings },
      { to: routes.channels, labelKey: 'nav.channels', icon: IconBell },
    ],
  },
];

/** Screens reachable from the header menu rather than the navbar, so they still get an icon. */
const ASIDE: { to: string; icon: TablerIcon; section: SectionId }[] = [
  { to: routes.profile, icon: IconUser, section: 'work' },
];

type Match = { icon: TablerIcon; color: string; section: SectionId; to: string };

const CANDIDATES: Match[] = [
  ...NAV_SECTIONS.flatMap((section) =>
    section.items.map((item) => ({
      icon: item.icon,
      color: SECTION_COLOR[section.id],
      section: section.id,
      to: item.to,
    })),
  ),
  ...ASIDE.map((entry) => ({
    icon: entry.icon,
    color: SECTION_COLOR[entry.section],
    section: entry.section,
    to: entry.to,
  })),
  // Longest path first, so /settings/channels is not claimed by /settings.
].sort((a, b) => b.to.length - a.to.length);

/**
 * Which navigation entry a pathname belongs to — the one whose route it is, or the deepest one it
 * hangs off (an entity's timeline belongs to Entities, a responsibility's compliance to Compliance).
 */
export function activeNav(pathname: string): Match | null {
  return CANDIDATES.find((entry) => pathname === entry.to || pathname.startsWith(`${entry.to}/`)) ?? null;
}
