/**
 * Every path the SPA routes on, in one place.
 *
 * The router declares these, the nav links them, guards redirect to them, and reports build
 * drill-through URLs out of them — four places that used to spell each path by hand. A renamed screen
 * is now a compile error in the places that matter rather than a link that silently lands on the
 * board.
 *
 * Paths with parameters come as functions so a caller cannot forget to interpolate one.
 */
export const routes = {
  login: '/login',
  loginComplete: '/login/complete',
  changePassword: '/change-password',

  board: '/board',
  work: '/work',
  profile: '/profile',

  entities: '/entities',
  entityTimeline: (entityId: string) => `/entities/${entityId}/timeline`,

  dashboard: '/dashboard',
  entityHealth: '/reports/entity-health',
  neglect: '/reports/neglect',
  blocked: '/reports/blocked',

  compliance: '/insights/compliance',
  responsibilityCompliance: (responsibilityId: string) => `/insights/compliance/${responsibilityId}`,
  reliability: '/insights/reliability',
  concentration: '/insights/concentration',
  holdAging: '/insights/hold-aging',

  responsibilities: '/responsibilities',
  departments: '/departments',
  users: '/users',
  settings: '/settings',
  channels: '/settings/channels',

  /** The import wizard takes `?kind=entities|workitems`, which is how empty states link into it. */
  import: '/import',

  /**
   * The integrator screens moved into Settings as tabs. These paths stay declared so old bookmarks
   * and external links keep landing somewhere sensible: the router redirects each one to its tab.
   * New code should link with {@link settingsTabLink} instead.
   */
  entityFields: '/settings/entity-fields',
  apiKeys: '/settings/api-keys',
  webhooks: '/settings/webhooks',

  /** The in-app user manual. Bare /help opens the first topic. */
  help: '/help',
  helpTopic: (slug: string) => `/help/${slug}`,
} as const;

/** Where an empty entities or work list sends somebody who has a spreadsheet. */
export function importLink(kind: 'entities' | 'workitems'): string {
  return `${routes.import}?kind=${kind}`;
}

/** The query parameter the settings page reads to open one of its tabs. */
export const SETTINGS_TAB_PARAM = 'tab';

/** The tabs of the settings page. `general` is the default and is kept out of the URL. */
export const SETTINGS_TABS = ['general', 'custom-fields', 'api-keys', 'webhooks'] as const;

export type SettingsTab = (typeof SETTINGS_TABS)[number];

export function isSettingsTab(value: string | null): value is SettingsTab {
  return SETTINGS_TABS.includes(value as SettingsTab);
}

/** A deep link to one settings tab — what the old integrator routes redirect to. */
export function settingsTabLink(tab: SettingsTab): string {
  return tab === 'general' ? routes.settings : `${routes.settings}?${SETTINGS_TAB_PARAM}=${tab}`;
}

/** Route patterns, which differ from paths only where a parameter is declared. */
export const routePatterns = {
  entityTimeline: '/entities/:entityId/timeline',
  responsibilityCompliance: '/insights/compliance/:responsibilityId',
  helpTopic: '/help/:slug',
  catchAll: '*',
} as const;

/** The query parameter the work list reads to open a single item's drawer. */
export const WORK_ITEM_PARAM = 'workItemId';

/** The work list, opened on one item — what a notification links to. */
export function workItemLink(workItemId: string): string {
  return `${routes.work}?${WORK_ITEM_PARAM}=${workItemId}`;
}
