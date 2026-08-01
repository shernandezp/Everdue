import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Card,
  Divider,
  Drawer,
  Group,
  Loader,
  Modal,
  Stack,
  Text,
  Textarea,
  Timeline,
  Title,
  Tooltip,
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import {
  IconArrowBackUp,
  IconArrowsExchange,
  IconCalendarEvent,
  IconCheck,
  IconCircleCheck,
  IconCircleX,
  IconMessage,
  IconMessagePlus,
  IconPencil,
  IconPlayerPause,
  IconPlayerPlay,
  IconPlus,
  IconTrash,
  type TablerIcon,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { WorkItemEventType } from '../../api/types';
import { StatusBadge } from '../../components/StatusBadge';
import { STATUS_COLOR, STATUS_ICON } from '../../theme';
import { formatDate, formatDateTime, toDateInputValue } from '../../lib/format';
import { useSession } from '../auth/session';
import { AttachmentsPanel } from './AttachmentsPanel';
import { ChecklistPanel } from './ChecklistPanel';
import { EditWorkItemModal } from './EditWorkItemModal';
import { HoldDialog } from './HoldDialog';
import { MentionPicker } from './MentionPicker';
import { useComments, useWorkItemActions, useWorkItemDetail } from './hooks';
import { routes } from '../../lib/routes';

/**
 * The item detail is a drawer over whatever the user was doing, not a page: keeping their work
 * context is the difference between a board people use and one they abandon.
 */
export function WorkItemDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t } = useTranslation();
  const { user, isAdmin } = useSession();
  const detail = useWorkItemDetail(id);
  const actions = useWorkItemActions();
  const comments = useComments(id);

  const [holdOpen, setHoldOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [newDueDate, setNewDueDate] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const [comment, setComment] = useState('');
  const [mentioned, setMentioned] = useState<string[]>([]);

  const item = detail.data?.item;
  const allowed = detail.data?.allowedTransitions ?? [];
  const isOwnerOrAdmin = isAdmin || item?.ownerUserId === user?.id;

  const requirements = detail.data?.completionRequirements;
  const checklistTotal = item?.checklistTotal ?? 0;

  // The states in which a checklist may still be worked. Once an item is completed or cancelled its list is part
  // of the record; reopening makes it editable again without clearing what was ticked.
  const isWorkable =
    item?.status === 'Open' || item?.status === 'InProgress' || item?.status === 'OnHold' || item?.status === 'Missed';

  // Assembled from the same two facts the server refuses on, so the tooltip and the 409 always agree.
  const completionBlockedReason = (() => {
    if (!requirements) return null;

    const reasons: string[] = [];

    if (requirements.requiredChecklistOpen > 0) {
      reasons.push(t('checklist.blockedByItems', { count: requirements.requiredChecklistOpen }));
    }

    if (requirements.attachmentRequired && requirements.attachmentCount === 0) {
      reasons.push(t('checklist.blockedByProof'));
    }

    return reasons.length > 0 ? reasons.join(' · ') : null;
  })();

  const run = (action: Parameters<typeof actions.mutate>[0]['action']) => {
    if (!id) return;
    actions.mutate({ id, action });
  };

  return (
    <Drawer opened={id !== null} onClose={onClose} position="right" size="lg" title={t('workItem.detail')}>
      {detail.isLoading && <Loader />}

      {item && (
        <Stack gap="md">
          <Stack gap={4}>
            <Title order={4}>{item.title}</Title>
            <Group gap="xs">
              <StatusBadge
                status={item.status}
                isOverdue={item.isOverdue}
                holdReason={item.holdReason}
                holdReasonText={item.holdReasonText}
              />
              <Badge variant="dot" color="gray">
                {item.responsibilityId ? t('workItem.occurrence') : t('workItem.oneOff')}
              </Badge>
            </Group>
          </Stack>

          {item.description && <Text size="sm">{item.description}</Text>}

          <Card withBorder padding="sm">
            <Stack gap={6}>
              <Field label={t('workItem.owner')} value={item.ownerDisplayName} />
              <Field
                label={t('workItem.entity')}
                value={
                  item.entityId ? (
                    <Link to={routes.entityTimeline(item.entityId)} onClick={onClose}>
                      {item.entityName}
                    </Link>
                  ) : (
                    t('common.none')
                  )
                }
              />
              <Field label={t('workItem.department')} value={item.departmentName ?? t('common.none')} />
              <Field label={t('workItem.dueDate')} value={formatDateTime(item.dueDate)} />
              {item.periodStart && item.periodEnd && (
                <Field
                  label={t('workItem.period')}
                  value={`${formatDate(item.periodStart)} → ${formatDate(item.periodEnd)}`}
                />
              )}
              {item.responsibilityTitle && (
                <Field label={t('workItem.responsibility')} value={item.responsibilityTitle} />
              )}
              {item.completedAt && (
                <Field
                  label={t('workItem.completed')}
                  value={`${formatDateTime(item.completedAt)} · ${item.completedByDisplayName ?? ''}`}
                />
              )}
            </Stack>
          </Card>

          <Group gap="xs">
            {(allowed.includes('Completed') || allowed.includes('CompletedLate')) && (
              /*
               * Disabled with the reason rather than hidden: `Completed` *is* a legal transition, and the server
               * refuses it with a 409 naming what is missing. Reporting the requirement here means nobody has to
               * discover the rule by being turned away.
               */
              <Tooltip label={completionBlockedReason ?? ''} disabled={!completionBlockedReason} withArrow>
                {/*
                  `data-disabled` rather than `disabled`: a genuinely disabled button swallows pointer events, so the
                  tooltip explaining *why* would never appear — which is the entire reason it is here. The click is
                  guarded instead, and the server refuses it anyway.
                */}
                <Button
                  size="xs"
                  color="teal"
                  leftSection={<IconCircleCheck size={14} />}
                  onClick={() => {
                    if (!completionBlockedReason) run({ kind: 'complete' });
                  }}
                  loading={actions.isPending}
                  data-disabled={completionBlockedReason ? true : undefined}
                  aria-disabled={completionBlockedReason ? true : undefined}
                >
                  {item.status === 'Missed' ? t('workItem.completeLate') : t('workItem.complete')}
                </Button>
              </Tooltip>
            )}

            {allowed.includes('InProgress') && (
              <Button
                size="xs"
                variant="light"
                color="indigo"
                leftSection={<IconPlayerPlay size={14} />}
                onClick={() => run({ kind: 'start' })}
                loading={actions.isPending}
              >
                {t('workItem.start')}
              </Button>
            )}

            {allowed.includes('OnHold') && (
              <Button
                size="xs"
                variant="light"
                color="orange"
                leftSection={<IconPlayerPause size={14} />}
                onClick={() => setHoldOpen(true)}
              >
                {t('workItem.hold')}
              </Button>
            )}

            {allowed.includes('Open') && (
              <Button
                size="xs"
                variant="light"
                color="blue"
                leftSection={<IconArrowBackUp size={14} />}
                onClick={() => run({ kind: 'reopen' })}
                disabled={item.status !== 'OnHold' && !isOwnerOrAdmin}
              >
                {t('workItem.reopen')}
              </Button>
            )}

            {(item.status === 'Open' || item.status === 'OnHold') && (
              <Button
                size="xs"
                variant="light"
                color="grape"
                leftSection={<IconCalendarEvent size={14} />}
                onClick={() => {
                  setNewDueDate(toDateInputValue(new Date(item.dueDate)));
                  setRescheduleOpen(true);
                }}
              >
                {t('workItem.reschedule')}
              </Button>
            )}

            {/* Anyone may edit; the change is attributed in the history below. */}
            <Button size="xs" variant="subtle" leftSection={<IconPencil size={14} />} onClick={() => setEditOpen(true)}>
              {t('common.edit')}
            </Button>

            {allowed.includes('Cancelled') && (
              <Button
                size="xs"
                variant="subtle"
                color="red"
                leftSection={<IconCircleX size={14} />}
                onClick={() => run({ kind: 'cancel' })}
              >
                {t('workItem.cancel')}
              </Button>
            )}
          </Group>

          {(checklistTotal > 0 || isWorkable) && (
            <>
              <Divider label={t('checklist.title')} labelPosition="left" />
              <ChecklistPanel workItemId={item.id} editable={isWorkable} />
            </>
          )}

          <Divider label={t('attachments.title')} labelPosition="left" />

          {requirements?.attachmentRequired && requirements.attachmentCount === 0 && (
            <Text size="xs" c="orange">
              {t('checklist.proofRequired')}
            </Text>
          )}

          <AttachmentsPanel workItemId={item.id} />

          <Divider label={t('workItem.comments')} labelPosition="left" />

          <Stack gap="xs">
            {(comments.list.data ?? []).map((entry) => (
              <Card key={entry.id} withBorder padding="xs">
                <Group justify="space-between" align="flex-start" wrap="nowrap">
                  <Stack gap={2}>
                    <Text size="xs" c="dimmed">
                      {entry.userDisplayName} · {formatDateTime(entry.createdAt)}
                    </Text>
                    <Text size="sm">{entry.body}</Text>
                  </Stack>
                  {(isAdmin || entry.userId === user?.id) && (
                    <ActionIcon
                      variant="subtle"
                      color="red"
                      aria-label={t('workItem.deleteComment')}
                      onClick={() => comments.remove.mutate(entry.id)}
                    >
                      <IconTrash size={16} />
                    </ActionIcon>
                  )}
                </Group>
              </Card>
            ))}

            {(comments.list.data ?? []).length === 0 && (
              <Text size="sm" c="dimmed">
                {t('workItem.noComments')}
              </Text>
            )}

            <Textarea
              placeholder={t('workItem.commentPlaceholder')}
              autosize
              minRows={2}
              value={comment}
              onChange={(event) => setComment(event.currentTarget.value)}
            />

            {/* Mentions are picked, not parsed: the body stays plain text and the ids travel beside it. */}
            <MentionPicker
              value={mentioned}
              onChange={setMentioned}
              onInsert={(name) => setComment((current) => `${current}${current.endsWith(' ') || current === '' ? '' : ' '}@${name} `)}
            />

            <Group justify="flex-end">
              <Button
                size="xs"
                leftSection={<IconMessagePlus size={14} />}
                disabled={comment.trim().length === 0}
                loading={comments.add.isPending}
                onClick={() => {
                  comments.add.mutate({ body: comment.trim(), mentionedUserIds: mentioned });
                  setComment('');
                  setMentioned([]);
                }}
              >
                {t('workItem.addComment')}
              </Button>
            </Group>
          </Stack>

          <Divider label={t('workItem.history')} labelPosition="left" />

          <Timeline bulletSize={22} lineWidth={2}>
            {(detail.data?.events ?? []).map((event) => {
              // A status change wears the colour and glyph of the status it arrived at; everything
              // else wears its own. See EVENT_GLYPH.
              const glyph =
                event.eventType === 'StatusChanged' && event.toStatus
                  ? { icon: STATUS_ICON[event.toStatus], colour: STATUS_COLOR[event.toStatus] }
                  : EVENT_GLYPH[event.eventType];

              const EventIcon = glyph.icon;

              return (
                <Timeline.Item
                  key={event.id}
                  color={glyph.colour}
                  bullet={<EventIcon size={12} />}
                  title={
                    event.eventType === 'StatusChanged' && event.toStatus
                      ? `${event.fromStatus ? t(`status.${event.fromStatus}`) : ''} → ${t(`status.${event.toStatus}`)}`
                      : t(`event.${event.eventType}`)
                  }
                >
                  <Text size="xs" c="dimmed">
                    {formatDateTime(event.timestamp)} · {event.userDisplayName ?? t('event.byEngine')}
                  </Text>

                  {/* Which fields an edit touched — the point of allowing anyone to make it. */}
                  {(event.eventType === 'Updated' || event.eventType === 'Reassigned') &&
                    changedFieldLabels(event.dataJson, t).length > 0 && (
                      <Text size="xs" c="dimmed">
                        {changedFieldLabels(event.dataJson, t).join(', ')}
                      </Text>
                    )}

                  {/* A reschedule is only meaningful as the two dates: "moved" says nothing on its own. */}
                  {event.eventType === 'Rescheduled' && rescheduleDates(event.dataJson) && (
                    <Badge size="xs" variant="light" color="grape">
                      {rescheduleDates(event.dataJson)}
                    </Badge>
                  )}
                </Timeline.Item>
              );
            })}
          </Timeline>
        </Stack>
      )}

      <HoldDialog
        opened={holdOpen}
        onClose={() => setHoldOpen(false)}
        busy={actions.isPending}
        onConfirm={(reason, text) => run({ kind: 'hold', reason, text })}
      />

      {item && <EditWorkItemModal item={item} opened={editOpen} onClose={() => setEditOpen(false)} />}

      <Modal opened={rescheduleOpen} onClose={() => setRescheduleOpen(false)} title={t('workItem.reschedule')} centered>
        <Stack>
          {item?.responsibilityId && <Alert color="blue">{t('workItem.occurrenceRescheduleHint')}</Alert>}

          <DatePickerInput label={t('workItem.newDueDate')} value={newDueDate} onChange={setNewDueDate} />
          <Textarea
            label={t('workItem.note')}
            autosize
            minRows={2}
            value={note}
            onChange={(event) => setNote(event.currentTarget.value)}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={() => setRescheduleOpen(false)}>
              {t('common.cancel')}
            </Button>
            <Button
              loading={actions.isPending}
              disabled={!newDueDate}
              leftSection={<IconCheck size={16} />}
              onClick={() => {
                if (!newDueDate) return;
                const target = new Date(`${newDueDate}T23:59:59`);
                run({ kind: 'reschedule', newDueDate: target.toISOString(), note: note.trim() || null });
                setRescheduleOpen(false);
                setNote('');
              }}
            >
              {t('common.confirm')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Drawer>
  );
}

/** A glyph and a colour per kind of history entry, so the timeline can be read down its left edge. */
const EVENT_GLYPH: Record<WorkItemEventType, { icon: TablerIcon; colour: string }> = {
  Created: { icon: IconPlus, colour: 'everdue' },
  StatusChanged: { icon: IconCircleCheck, colour: 'everdue' },
  Rescheduled: { icon: IconCalendarEvent, colour: 'grape' },
  CommentAdded: { icon: IconMessage, colour: 'cyan' },
  Updated: { icon: IconPencil, colour: 'gray' },
  Reassigned: { icon: IconArrowsExchange, colour: 'violet' },
};

/** Field names as they appear in an Updated event's payload, mapped to their translated labels. */
const CHANGED_FIELD_LABELS: Record<string, string> = {
  title: 'workItem.title',
  description: 'workItem.description',
  ownerUserId: 'workItem.owner',
  entityId: 'workItem.entity',
  departmentId: 'workItem.department',
};

/**
 * The old and new due dates out of a `Rescheduled` event's payload. The dates have been written
 * since v1; only the reading of them is new.
 */
function rescheduleDates(dataJson: string | null | undefined): string | null {
  if (!dataJson) return null;

  try {
    const payload = JSON.parse(dataJson) as { from?: string; to?: string };
    if (!payload.from || !payload.to) return null;

    return `${formatDate(payload.from)} → ${formatDate(payload.to)}`;
  } catch {
    return null;
  }
}

function changedFieldLabels(dataJson: string | null | undefined, t: (key: string) => string): string[] {
  if (!dataJson) return [];

  try {
    const payload = JSON.parse(dataJson) as { changes?: { field?: string }[] };

    return (payload.changes ?? [])
      .map((change) => change.field)
      .filter((field): field is string => Boolean(field && CHANGED_FIELD_LABELS[field]))
      .map((field) => t(CHANGED_FIELD_LABELS[field]));
  } catch {
    // The payload is free-form by design; a shape this build does not know about is not an error.
    return [];
  }
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Group gap="xs" wrap="nowrap" align="flex-start">
      <Text size="xs" c="dimmed" w={110} style={{ flexShrink: 0 }}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {value}
      </Text>
    </Group>
  );
}
