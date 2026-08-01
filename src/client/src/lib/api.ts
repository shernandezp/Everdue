import { ApiError, http, query, readProblem } from './http';
import type {
  About,
  ApiKey,
  ApiKeyScope,
  Attachment,
  AuthProviders,
  BlockedByEntityGroup,
  BulkResult,
  ChannelHealth,
  ChannelSettings,
  ChannelTestResult,
  ChecklistItem,
  ChecklistTemplateItem,
  ChronicResponsibility,
  Comment,
  ComplianceRow,
  ConcentrationSeries,
  CreatedApiKey,
  CreatedWebhook,
  CurrentUser,
  DemoModeResult,
  DemoStatus,
  DepartmentDto,
  DigestFrequency,
  DigestSubscription,
  EntityDto,
  EntityFieldDef,
  EntityHealthRow,
  EntityTimeline,
  ExceptionsReport,
  HoldAging,
  HoldReason,
  ImportPreview,
  ImportResult,
  Language,
  NeglectRow,
  NotificationChannel,
  NotificationDto,
  NotificationPreferences,
  Paged,
  ReassignResult,
  ReliabilityRow,
  ResponsibilityCompliance,
  ResponsibilityDto,
  ResponsibilityEventDto,
  SavedView,
  TelegramLink,
  TenantSettings,
  UnreadCount,
  UserDto,
  WebhookEventType,
  WebhookHealth,
  WebhookSubscription,
  WorkItem,
  WorkItemDetail,
} from '../api/types';

/**
 * Multipart POST with an optional set of extra form fields. Used by the import wizard, whose commit re-posts the
 * file alongside the confirmed mapping — there is deliberately no server-side temporary state between the steps.
 */
async function postForm<T>(path: string, file: File, fields: Record<string, string> = {}): Promise<T> {
  const form = new FormData();
  form.append('file', file);

  for (const [key, value] of Object.entries(fields)) {
    form.append(key, value);
  }

  const response = await fetch(`/api/v1${path}`, {
    method: 'POST',
    credentials: 'same-origin',
    body: form,
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  return (await response.json()) as T;
}

export type WorkItemFilters = {
  view?: 'board' | 'list';
  ownerId?: string;
  departmentId?: string;
  entityId?: string;

  /** Set by the insight drill-throughs. The work list forwards every URL parameter as-is. */
  responsibilityId?: string;
  occurrences?: boolean;
  entityType?: string;
  status?: string;
  holdReason?: string;
  dueFrom?: string;
  dueTo?: string;
  completedFrom?: string;
  completedTo?: string;
  overdue?: boolean;
  includeCancelled?: boolean;
  search?: string;
  sort?: string;
  descending?: boolean;
  page?: number;
  pageSize?: number;
};

export type ReportFilters = {
  ownerId?: string;
  departmentId?: string;
  entityType?: string;
  from?: string;
  to?: string;
};

/** Which work an insight is about. */
export type InsightScope = ReportFilters & { entityId?: string };

/**
 * The scope plus the trend window. Omitting from/to asks for the rolling window — the last `buckets`
 * buckets, the newest of which is still in progress.
 */
export type InsightFilters = InsightScope & {
  bucket?: BucketKind;
  buckets?: number;
};

export type BucketKind = 'Week' | 'Month';

/** One place per resource. Screens never build a URL by hand. */
export const api = {
  auth: {
    me: () => http.get<CurrentUser>('/auth/me'),
    login: (email: string, password: string) => http.post<CurrentUser>('/auth/login', { email, password }),
    logout: () => http.post<void>('/auth/logout'),
    changePassword: (currentPassword: string, newPassword: string) =>
      http.post<void>('/auth/password', { currentPassword, newPassword }),
    updateProfile: (displayName: string, preferredLanguage: string | null) =>
      http.put<CurrentUser>('/auth/profile', { displayName, preferredLanguage }),

    /// Which sign-in methods this installation offers, so the login screen only shows what works.
    providers: () => http.get<AuthProviders>('/auth/providers'),
  },

  entities: {
    list: (params: { search?: string; type?: string; includeInactive?: boolean; pageSize?: number } = {}) =>
      http.get<Paged<EntityDto>>(`/entities${query({ ...params, pageSize: params.pageSize ?? 100 })}`),
    get: (id: string) => http.get<EntityDto>(`/entities/${id}`),
    /**
     * `customFields` is keyed by definition id. Omitting it leaves stored values alone; sending a key with an
     * empty value clears that one field.
     */
    create: (body: { name: string; type: string; customFields?: Record<string, string> }) =>
      http.post<EntityDto>('/entities', body),
    update: (
      id: string,
      body: { name: string; type: string; active: boolean; customFields?: Record<string, string> },
    ) => http.put<EntityDto>(`/entities/${id}`, body),
    deactivate: (id: string) => http.del<EntityDto>(`/entities/${id}`),
  },

  departments: {
    list: (params: { search?: string; includeInactive?: boolean; pageSize?: number } = {}) =>
      http.get<Paged<DepartmentDto>>(`/departments${query({ ...params, pageSize: params.pageSize ?? 100 })}`),
    create: (body: { name: string }) => http.post<DepartmentDto>('/departments', body),
    update: (id: string, body: { name: string; active: boolean }) => http.put<DepartmentDto>(`/departments/${id}`, body),
    deactivate: (id: string) => http.del<DepartmentDto>(`/departments/${id}`),
  },

  responsibilities: {
    list: (params: { search?: string; includeInactive?: boolean; pageSize?: number } = {}) =>
      http.get<Paged<ResponsibilityDto>>(`/responsibilities${query({ ...params, pageSize: params.pageSize ?? 100 })}`),
    get: (id: string) => http.get<ResponsibilityDto>(`/responsibilities/${id}`),
    events: (id: string) => http.get<ResponsibilityEventDto[]>(`/responsibilities/${id}/events`),
    create: (body: Record<string, unknown>) => http.post<ResponsibilityDto>('/responsibilities', body),
    update: (id: string, body: Record<string, unknown>) => http.put<ResponsibilityDto>(`/responsibilities/${id}`, body),
    deactivate: (id: string) => http.del<ResponsibilityDto>(`/responsibilities/${id}`),
    pause: (id: string, until: string) => http.post<ResponsibilityDto>(`/responsibilities/${id}/pause`, { until }),
    resume: (id: string) => http.post<ResponsibilityDto>(`/responsibilities/${id}/resume`),

    // Future occurrences follow the responsibility's owner automatically; existing ones on request.
    reassign: (id: string, body: { newOwnerUserId: string; applyToWorkableOccurrences: boolean }) =>
      http.post<ReassignResult>(`/responsibilities/${id}/reassign`, body),
  },

  workItems: {
    list: (filters: WorkItemFilters) => http.get<Paged<WorkItem>>(`/workitems${query(filters)}`),
    get: (id: string) => http.get<WorkItemDetail>(`/workitems/${id}`),
    create: (body: {
      title: string;
      description?: string | null;
      ownerUserId: string;
      entityId?: string | null;
      departmentId?: string | null;
      dueDate: string;
    }) => http.post<WorkItem>('/workitems', body),
    update: (
      id: string,
      body: {
        title: string;
        description?: string | null;
        ownerUserId: string;
        entityId?: string | null;
        departmentId?: string | null;
      },
    ) => http.put<WorkItem>(`/workitems/${id}`, body),

    // One endpoint per legal move: the transition matrix lives on the server, never in the client.
    start: (id: string) => http.post<WorkItem>(`/workitems/${id}/start`),
    complete: (id: string) => http.post<WorkItem>(`/workitems/${id}/complete`),
    hold: (id: string, reason: HoldReason, text?: string | null) =>
      http.post<WorkItem>(`/workitems/${id}/hold`, { reason, text: text ?? null }),
    reopen: (id: string) => http.post<WorkItem>(`/workitems/${id}/reopen`),
    cancel: (id: string) => http.post<WorkItem>(`/workitems/${id}/cancel`),
    reschedule: (id: string, newDueDate: string, note?: string | null) =>
      http.post<WorkItem>(`/workitems/${id}/reschedule`, { newDueDate, note: note ?? null }),

    comments: (id: string) => http.get<Comment[]>(`/workitems/${id}/comments`),

    // Mentions are picked, not parsed: the body stays plain text and the ids travel beside it.
    addComment: (id: string, body: string, mentionedUserIds: string[] = []) =>
      http.post<Comment>(`/workitems/${id}/comments`, { body, mentionedUserIds }),
    deleteComment: (commentId: string) => http.del<void>(`/comments/${commentId}`),

    // One round trip, per-item results: the server runs each id through the same single-item command.
    bulk: (body: {
      ids: string[];
      action: 'Complete' | 'Reassign' | 'Reschedule';
      ownerUserId?: string | null;
      newDueDate?: string | null;
      note?: string | null;
    }) => http.post<BulkResult>('/workitems/bulk', body),

    attachments: (id: string) => http.get<Attachment[]>(`/workitems/${id}/attachments`),
  },

  attachments: {
    // Multipart, so it bypasses the JSON helper. Same cookie, same origin.
    upload: async (workItemId: string, file: File) => {
      const form = new FormData();
      form.append('file', file);

      const response = await fetch(`/api/v1/workitems/${workItemId}/attachments`, {
        method: 'POST',
        credentials: 'same-origin',
        body: form,
      });

      if (!response.ok) {
        throw new ApiError(response.status, await readProblem(response));
      }

      return (await response.json()) as Attachment;
    },
    downloadUrl: (id: string) => `/api/v1/attachments/${id}`,
    remove: (id: string) => http.del<void>(`/attachments/${id}`),
  },

  notifications: {
    list: (params: { unreadOnly?: boolean; pageSize?: number } = {}) =>
      http.get<Paged<NotificationDto>>(`/notifications${query({ ...params, pageSize: params.pageSize ?? 20 })}`),
    unreadCount: () => http.get<UnreadCount>('/notifications/unread-count'),
    markRead: (ids?: string[]) => http.post<UnreadCount>('/notifications/read', { ids: ids ?? null }),
  },

  me: {
    preferences: () => http.get<NotificationPreferences>('/me/notification-preferences'),
    savePreferences: (body: { channel: NotificationChannel | null; types: Record<string, boolean> }) =>
      http.put<NotificationPreferences>('/me/notification-preferences', body),
    startTelegramLink: () => http.post<TelegramLink>('/me/telegram/link'),
    unlinkTelegram: () => http.del<NotificationPreferences>('/me/telegram/link'),
  },

  digestSubscriptions: {
    list: () => http.get<DigestSubscription[]>('/digest-subscriptions'),
    save: (body: {
      frequency: DigestFrequency;
      weeklyDayOfWeek: string;
      departmentId?: string | null;
      active: boolean;
    }) => http.put<DigestSubscription>('/digest-subscriptions', body),
  },

  channels: {
    list: () => http.get<ChannelSettings[]>('/settings/channels'),
    health: () => http.get<ChannelHealth[]>('/settings/channels/health'),
    save: (channel: NotificationChannel, configJson: string, active: boolean) =>
      http.put<ChannelSettings>(`/settings/channels/${channel}`, { configJson, active }),
    remove: (channel: NotificationChannel) => http.del<void>(`/settings/channels/${channel}`),
    test: (channel: NotificationChannel) => http.post<ChannelTestResult>(`/settings/channels/${channel}/test`),
  },

  savedViews: {
    list: () => http.get<SavedView[]>('/saved-views'),
    save: (body: { name: string; route: string; queryString: string }) => http.post<SavedView>('/saved-views', body),
    remove: (id: string) => http.del<void>(`/saved-views/${id}`),
  },

  reports: {
    exceptions: (filters: ReportFilters = {}) => http.get<ExceptionsReport>(`/reports/exceptions${query(filters)}`),
    entityHealth: (
      filters: ReportFilters & { sort?: string; descending?: boolean; search?: string; pageSize?: number } = {},
    ) => http.get<Paged<EntityHealthRow>>(`/reports/entity-health${query({ ...filters, pageSize: filters.pageSize ?? 100 })}`),
    neglect: (filters: ReportFilters & { days?: number } = {}) =>
      http.get<NeglectRow[]>(`/reports/neglect${query({ ...filters, days: filters.days ?? 90 })}`),
    blockedByEntity: (filters: ReportFilters = {}) =>
      http.get<BlockedByEntityGroup[]>(`/reports/blocked-by-entity${query(filters)}`),
    timeline: (entityId: string, filters: { from?: string; to?: string } = {}) =>
      http.get<EntityTimeline>(`/reports/entities/${entityId}/timeline${query(filters)}`),
  },

  /**
   * The intelligence layer. Every one of these is computed from the occurrence ledger on read, so the
   * filters are the whole request: there is nothing stored to get out of date.
   */
  insights: {
    compliance: (filters: InsightFilters & { sort?: string; descending?: boolean; pageSize?: number } = {}) =>
      http.get<Paged<ComplianceRow>>(`/insights/compliance${query({ ...filters, pageSize: filters.pageSize ?? 100 })}`),
    responsibility: (responsibilityId: string, filters: InsightFilters = {}) =>
      http.get<ResponsibilityCompliance>(`/insights/responsibilities/${responsibilityId}/compliance${query(filters)}`),
    reliability: (filters: InsightFilters & { sort?: string; descending?: boolean } = {}) =>
      http.get<ReliabilityRow[]>(`/insights/reliability${query(filters)}`),
    concentration: (filters: InsightFilters = {}) =>
      http.get<ConcentrationSeries>(`/insights/concentration${query(filters)}`),
    holdAging: (filters: InsightFilters = {}) => http.get<HoldAging>(`/insights/hold-aging${query(filters)}`),

    // No window: chronic judges each responsibility's own last N periods, so there is nothing to
    // bucket and the endpoint does not pretend otherwise.
    chronic: (filters: InsightScope & { limit?: number } = {}) =>
      http.get<ChronicResponsibility[]>(`/insights/chronic${query(filters)}`),
  },

  users: {
    list: () => http.get<UserDto[]>('/users?includeInactive=true'),
    create: (body: { email: string; password: string; displayName: string; role: string; preferredLanguage?: string | null }) =>
      http.post<UserDto>('/users', body),
    update: (
      id: string,
      body: {
        displayName: string;
        role: string;
        preferredLanguage?: string | null;
        active: boolean;
        whatsAppPhoneE164?: string | null;
      },
    ) => http.put<UserDto>(`/users/${id}`, body),
    resetPassword: (id: string, newPassword: string) => http.post<void>(`/users/${id}/password`, { newPassword }),

    // The departure path: everything this person owns becomes somebody else's, in one call.
    reassignAll: (id: string, body: { toUserId: string; includeResponsibilities: boolean; includeWorkableItems: boolean }) =>
      http.post<ReassignResult>(`/users/${id}/reassign-all`, body),
  },


  checklists: {
    forItem: (workItemId: string) => http.get<ChecklistItem[]>(`/workitems/${workItemId}/checklist`),
    add: (workItemId: string, text: string) =>
      http.post<ChecklistItem>(`/workitems/${workItemId}/checklist`, { text }),

    // Two endpoints rather than a boolean body: the server owns what checking means, and "check" and "uncheck"
    // read as the two things a person actually does.
    setChecked: (workItemId: string, itemId: string, checked: boolean) =>
      http.post<ChecklistItem>(`/workitems/${workItemId}/checklist/${itemId}/${checked ? 'check' : 'uncheck'}`),
    remove: (workItemId: string, itemId: string) =>
      http.del<void>(`/workitems/${workItemId}/checklist/${itemId}`),

    template: (responsibilityId: string) =>
      http.get<ChecklistTemplateItem[]>(`/responsibilities/${responsibilityId}/checklist-template`),

    // A wholesale replace: reordering, renaming and deleting always arrive together from the form.
    saveTemplate: (responsibilityId: string, items: { text: string; required: boolean }[]) =>
      http.put<ChecklistTemplateItem[]>(`/responsibilities/${responsibilityId}/checklist-template`, { items }),
  },

  entityFields: {
    list: (params: { entityType?: string; includeInactive?: boolean } = {}) =>
      http.get<EntityFieldDef[]>(`/entity-fields${query(params)}`),
    create: (body: { entityType: string; name: string; fieldType: string; options?: string[] | null }) =>
      http.post<EntityFieldDef>('/entity-fields', body),
    update: (id: string, body: { name: string; options?: string[] | null; position: number; active: boolean }) =>
      http.put<EntityFieldDef>(`/entity-fields/${id}`, body),
    remove: (id: string) => http.del<void>(`/entity-fields/${id}`),
  },

  /**
   * Export URLs, not fetches: the browser downloads them, so the button is a link and there is no blob to hold
   * in memory. Every one carries the page's own filters, which is what makes the file match the screen.
   */
  exports: {
    workItems: (filters: WorkItemFilters) => `/api/v1/exports/workitems${query(filters)}`,
    report: (view: 'entity-health' | 'neglect' | 'blocked-by-entity', filters: ReportFilters & { days?: number }) =>
      `/api/v1/exports/reports/${view}${query(filters)}`,
    insight: (view: 'compliance' | 'reliability' | 'concentration' | 'hold-aging', filters: InsightFilters) =>
      `/api/v1/exports/insights/${view}${query(filters)}`,
    raw: (table: string) => `/api/v1/exports/raw/${table}`,
  },

  imports: {
    preview: (kind: 'entities' | 'workitems', file: File) => postForm<ImportPreview>(`/imports/${kind}/preview`, file),
    commit: (kind: 'entities' | 'workitems', file: File, mapping: Record<string, string>) =>
      postForm<ImportResult>(`/imports/${kind}/commit`, file, { mapping: JSON.stringify(mapping) }),
  },

  apiKeys: {
    list: (params: { includeRevoked?: boolean } = {}) => http.get<ApiKey[]>(`/api-keys${query(params)}`),

    // The response is the only time the token exists outside the server. The screen says so.
    create: (body: { name: string; scope: ApiKeyScope; actorUserId?: string | null; expiresAt?: string | null }) =>
      http.post<CreatedApiKey>('/api-keys', body),
    revoke: (id: string) => http.del<ApiKey>(`/api-keys/${id}`),
  },

  webhooks: {
    list: () => http.get<WebhookSubscription[]>('/webhooks'),
    health: () => http.get<WebhookHealth[]>('/webhooks/health'),
    create: (body: { url: string; eventTypes: WebhookEventType[] }) => http.post<CreatedWebhook>('/webhooks', body),
    update: (id: string, body: { url: string; eventTypes: WebhookEventType[]; active: boolean }) =>
      http.put<WebhookSubscription>(`/webhooks/${id}`, body),
    remove: (id: string) => http.del<void>(`/webhooks/${id}`),
    test: (id: string) => http.post<WebhookSubscription>(`/webhooks/${id}/test`),
  },

  meta: {
    // The server owns the supported-language list; the picker renders from it, so the two cannot disagree.
    languages: () => http.get<Language[]>('/languages'),
    about: () => http.get<About>('/about'),
  },

  settings: {
    get: () => http.get<TenantSettings>('/settings/tenant'),
    update: (body: {
      name: string;
      timeZoneId: string;
      digestHourLocal: number;
      defaultLanguage: string;
      reminderHourLocal: number;
      canUseSystemChannels: boolean;
    }) => http.put<TenantSettings>('/settings/tenant', body),
  },

  demo: {
    status: () => http.get<DemoStatus>('/settings/demo'),

    /**
     * Irreversible in both directions: the tenant is wiped either way, and only the caller's own account
     * survives. `confirmation` is the workspace name typed out and `password` is the caller's own — the
     * server checks both and returns 400 with the offending field, so the dialog can show it inline.
     */
    set: (body: { enabled: boolean; confirmation: string; password: string }) =>
      http.post<DemoModeResult>('/settings/demo', body),
  },
};

/** Turns a report's drill-through object into the board/list query string it describes. */
export function drillThroughToFilters(drill: { workItemQuery: Record<string, string> }): WorkItemFilters {
  return drill.workItemQuery as WorkItemFilters;
}
