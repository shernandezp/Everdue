import type { InsightFilters, ReportFilters, WorkItemFilters } from './api';

/**
 * React Query cache keys, built here rather than spelled at each call site.
 *
 * Invalidation is the reason this file exists. `invalidateQueries({ queryKey: keys.workItems.all })`
 * has to match the key a page actually queried with, and when both are inline string arrays in
 * different files, nothing catches the day they stop matching — the mutation succeeds, the list keeps
 * showing stale rows, and it looks like a server bug.
 *
 * Every group exposes `all` as its prefix. React Query matches keys by prefix, so invalidating `all`
 * clears every variation underneath it.
 */
export const keys = {
  session: {
    me: ['me'] as const,
    authProviders: ['auth-providers'] as const,
  },

  users: {
    all: ['users'] as const,
  },

  entities: {
    all: ['entities'] as const,
    picker: ['entities', 'picker'] as const,
    pickerSearch: (search: string) => ['entities', 'picker', search] as const,
    one: (id: string | null) => ['entities', 'one', id] as const,
    list: (filters: { search?: string; showInactive?: boolean }) => ['entities', filters] as const,
  },

  departments: {
    all: ['departments'] as const,
    picker: ['departments', 'picker'] as const,
    list: (filters: { includeInactive?: boolean }) => ['departments', filters] as const,
  },

  responsibilities: {
    all: ['responsibilities'] as const,
    one: (id: string | null) => ['responsibilities', 'one', id] as const,
    events: (id: string | null) => ['responsibilities', 'events', id] as const,
  },

  workItems: {
    all: ['workitems'] as const,
    list: (filters: WorkItemFilters) => ['workitems', filters] as const,

    /** A single item, kept separate from the lists so opening a drawer never refetches a page. */
    detail: ['workitem'] as const,

    /** Null when the drawer is closed: the key still has to exist for the disabled query. */
    one: (id: string | null) => ['workitem', id] as const,
  },

  comments: {
    forItem: (workItemId: string | null) => ['comments', workItemId] as const,
  },

  attachments: {
    forItem: (workItemId: string) => ['attachments', workItemId] as const,
  },

  savedViews: {
    all: ['saved-views'] as const,
  },

  reports: {
    all: ['reports'] as const,
    exceptions: (filters: ReportFilters) => ['reports', 'exceptions', filters] as const,
    entityHealth: (filters: ReportFilters, search: string, sort: unknown) =>
      ['reports', 'entity-health', filters, search, sort] as const,
    neglect: (filters: ReportFilters, days: number) => ['reports', 'neglect', filters, days] as const,
    blocked: (filters: ReportFilters) => ['reports', 'blocked', filters] as const,
    timeline: (entityId: string | undefined) => ['reports', 'timeline', entityId] as const,
  },

  insights: {
    all: ['insights'] as const,
    compliance: (filters: InsightFilters, sort: unknown) => ['insights', 'compliance', filters, sort] as const,
    responsibility: (responsibilityId: string | undefined, window: unknown) =>
      ['insights', 'responsibility', responsibilityId, window] as const,
    reliability: (filters: InsightFilters, sort: unknown) => ['insights', 'reliability', filters, sort] as const,
    concentration: (filters: InsightFilters) => ['insights', 'concentration', filters] as const,
    holdAging: (filters: InsightFilters) => ['insights', 'hold-aging', filters] as const,
    chronic: (limit: number) => ['insights', 'chronic', limit] as const,
  },

  notifications: {
    all: ['notifications'] as const,
    unreadCount: ['notifications', 'unread-count'] as const,
    list: ['notifications', 'list'] as const,
    preferences: ['notification-preferences'] as const,
    digestSubscriptions: ['digest-subscriptions'] as const,
  },

  channels: {
    all: ['channels'] as const,
    health: ['channels', 'health'] as const,
  },

  settings: {
    all: ['settings'] as const,
  },

  demo: {
    all: ['demo'] as const,
  },

  checklists: {
    forItem: (workItemId: string) => ['checklist', workItemId] as const,
    template: (responsibilityId: string | null) => ['checklist-template', responsibilityId] as const,
  },

  entityFields: {
    all: ['entity-fields'] as const,
  },

  apiKeys: {
    all: ['api-keys'] as const,
  },

  webhooks: {
    all: ['webhooks'] as const,
    health: ['webhooks', 'health'] as const,
  },

  meta: {
    languages: ['languages'] as const,
    about: ['about'] as const,
  },
} as const;
