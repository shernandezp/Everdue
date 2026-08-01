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

  /** v2.5. The import wizard takes `?kind=entities|workitems`, which is how empty states link into it. */
  import: '/import',
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
