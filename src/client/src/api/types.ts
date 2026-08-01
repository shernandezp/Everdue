import type { components } from './schema';

// Every API type in the client comes from the server's OpenAPI document (npm run gen:api).
// Nothing here is hand-written, so a contract change breaks the build instead of production.
type Schemas = components['schemas'];

export type WorkItemStatus = Schemas['WorkItemStatus'];
export type HoldReason = Schemas['HoldReason'];
export type EntityType = Schemas['EntityType'];
export type RecurrenceKind = Schemas['RecurrenceKind'];
export type UserRole = Schemas['UserRole'];
export type WorkItemEventType = Schemas['WorkItemEventType'];

export type CurrentUser = Schemas['CurrentUserDto'];
export type TenantSettings = Schemas['TenantSettingsDto'];
export type UserDto = Schemas['UserDto'];
export type EntityDto = Schemas['EntityDto'];
export type DepartmentDto = Schemas['DepartmentDto'];
export type ResponsibilityDto = Schemas['ResponsibilityDto'];
export type WorkItem = Schemas['WorkItemDto'];
export type WorkItemDetail = Schemas['WorkItemDetailDto'];
export type WorkItemEvent = Schemas['WorkItemEventDto'];
export type Comment = Schemas['CommentDto'];

export type ExceptionsReport = Schemas['ExceptionsReportDto'];
export type Metric = Schemas['MetricDto'];
export type HoldReasonGroup = Schemas['HoldReasonGroupDto'];
export type EntityHealthRow = Schemas['EntityHealthRowDto'];
export type NeglectRow = Schemas['NeglectRowDto'];
export type BlockedByEntityGroup = Schemas['BlockedByEntityGroupDto'];
export type EntityTimeline = Schemas['EntityTimelineDto'];
export type TimelineItem = Schemas['TimelineItemDto'];
export type DrillThrough = Schemas['DrillThrough'];

export type BucketPoint = Schemas['BucketPointDto'];
export type BucketAxis = Schemas['BucketAxisDto'];
export type ComplianceRow = Schemas['ComplianceRowDto'];
export type ResponsibilityCompliance = Schemas['ResponsibilityComplianceDto'];
export type StripPoint = Schemas['StripPointDto'];
export type ReliabilityRow = Schemas['ReliabilityRowDto'];
export type ConcentrationSeries = Schemas['ConcentrationSeriesDto'];
export type ConcentrationRow = Schemas['ConcentrationRowDto'];
export type HoldAging = Schemas['HoldAgingDto'];
export type HoldAgingRow = Schemas['HoldAgingRowDto'];
export type HoldAgingEntityRow = Schemas['HoldAgingEntityRowDto'];
export type ChronicResponsibility = Schemas['ChronicResponsibilityDto'];

export type NotificationType = Schemas['NotificationType'];
export type NotificationChannel = Schemas['NotificationChannel'];
export type DigestFrequency = Schemas['DigestFrequency'];

export type NotificationDto = Schemas['NotificationDto'];
export type UnreadCount = Schemas['UnreadCountDto'];
export type NotificationPreferences = Schemas['NotificationPreferencesDto'];
export type TelegramLink = Schemas['TelegramLinkDto'];
export type DigestSubscription = Schemas['DigestSubscriptionDto'];
export type ChannelSettings = Schemas['ChannelSettingsDto'];
export type ChannelHealth = Schemas['ChannelHealthDto'];
export type ChannelTestResult = Schemas['ChannelTestResultDto'];
export type Attachment = Schemas['AttachmentDto'];
export type SavedView = Schemas['SavedViewDto'];
export type BulkResult = Schemas['BulkResultDto'];
export type ReassignResult = Schemas['ReassignResultDto'];
export type AuthProviders = Schemas['AuthProvidersDto'];

// v2.5.
export type ChecklistItem = Schemas['ChecklistItemDto'];
export type ChecklistTemplateItem = Schemas['ChecklistTemplateItemDto'];
export type CompletionRequirements = Schemas['CompletionRequirementsDto'];
export type EntityFieldType = Schemas['EntityFieldType'];
export type EntityFieldDef = Schemas['EntityFieldDefDto'];
export type EntityCustomFieldValue = Schemas['EntityCustomFieldValueDto'];
export type ImportKind = Schemas['ImportKind'];
export type ImportField = Schemas['ImportFieldDto'];
export type ImportPreview = Schemas['ImportPreviewDto'];
export type ImportPreviewRow = Schemas['ImportPreviewRowDto'];
export type ImportResult = Schemas['ImportResultDto'];
export type ImportRowFailure = Schemas['ImportRowFailureDto'];
export type ApiKeyScope = Schemas['ApiKeyScope'];
export type ApiKey = Schemas['ApiKeyDto'];
export type CreatedApiKey = Schemas['CreatedApiKeyDto'];
export type WebhookEventType = Schemas['WebhookEventType'];
export type WebhookSubscription = Schemas['WebhookSubscriptionDto'];
export type CreatedWebhook = Schemas['CreatedWebhookDto'];
export type WebhookHealth = Schemas['WebhookHealthDto'];
export type Language = Schemas['LanguageDto'];
export type About = Schemas['AboutDto'];

export type DemoStatus = Schemas['DemoStatusDto'];
export type DemoModeResult = Schemas['DemoModeResultDto'];

export type Paged<T> = { items: T[]; totalCount: number; page: number; pageSize: number };

export const ENTITY_FIELD_TYPES: EntityFieldType[] = ['Text', 'Number', 'Date', 'Select'];

export const API_KEY_SCOPES: ApiKeyScope[] = ['ReadOnly', 'ReadWrite'];

/**
 * The six subscribable events. `Ping` is deliberately absent: it is what the test button sends, not something
 * a subscription asks for.
 */
export const WEBHOOK_EVENT_TYPES: WebhookEventType[] = [
  'WorkItemCreated',
  'WorkItemCompleted',
  'WorkItemMissed',
  'WorkItemOnHold',
  'WorkItemReassigned',
  'EntityCreated',
];

export const NOTIFICATION_TYPES: NotificationType[] = [
  'Assigned',
  'DueToday',
  'Missed',
  'Mentioned',
  'PutOnHold',
];

export const NOTIFICATION_CHANNELS: NotificationChannel[] = ['Email', 'Telegram', 'WhatsApp'];

export const DIGEST_FREQUENCIES: DigestFrequency[] = ['Daily', 'Weekly'];

export const WORK_ITEM_STATUSES: WorkItemStatus[] = [
  'Open',
  'InProgress',
  'OnHold',
  'Missed',
  'Completed',
  'CompletedLate',
  'Cancelled',
];

export const HOLD_REASONS: HoldReason[] = [
  'WaitingCustomer',
  'WaitingSupplier',
  'WaitingApproval',
  'MissingInformation',
  'Other',
];

export const ENTITY_TYPES: EntityType[] = ['Customer', 'Supplier', 'Equipment', 'Department', 'Company'];

export const RECURRENCE_KINDS: RecurrenceKind[] = ['Daily', 'WeeklyOnDays', 'MonthlyOnDay', 'Yearly'];

export const USER_ROLES: UserRole[] = ['Admin', 'Member'];
