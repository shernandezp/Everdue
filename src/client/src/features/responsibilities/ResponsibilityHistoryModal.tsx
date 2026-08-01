import { Loader, Modal, Text, Timeline } from '@mantine/core';
import {
  IconArrowsExchange,
  IconCircleX,
  IconPencil,
  IconPlayerPause,
  IconPlayerPlay,
  IconPlus,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ResponsibilityDto, ResponsibilityEventType } from '../../api/types';
import { api } from '../../lib/api';
import { formatDate, formatDateTime } from '../../lib/format';
import { keys } from '../../lib/queryKeys';

const EVENT_GLYPH: Record<ResponsibilityEventType, { icon: typeof IconPencil; colour: string }> = {
  Created: { icon: IconPlus, colour: 'teal' },
  Updated: { icon: IconPencil, colour: 'blue' },
  Reassigned: { icon: IconArrowsExchange, colour: 'violet' },
  Paused: { icon: IconPlayerPause, colour: 'orange' },
  Resumed: { icon: IconPlayerPlay, colour: 'teal' },
  Deactivated: { icon: IconCircleX, colour: 'red' },
};

/** Field names as they appear in a responsibility event's diff payload, mapped to translated labels. */
const FIELD_LABELS: Record<string, string> = {
  title: 'workItem.title',
  description: 'workItem.description',
  ownerUserId: 'workItem.owner',
  entityId: 'workItem.entity',
  departmentId: 'workItem.department',
  recurrenceKind: 'recurrence.kind',
  daysOfWeekMask: 'recurrence.weekdays',
  dayOfMonth: 'recurrence.dayOfMonth',
  monthOfYear: 'recurrence.monthOfYear',
  startDate: 'recurrence.startDate',
  active: 'common.active',
  requireChecklistToComplete: 'responsibility.requireChecklist',
  requireAttachmentToComplete: 'responsibility.requireAttachment',
};

/** Fields whose stored values are ids — the label alone is the readable part. */
const ID_FIELDS = new Set(['ownerUserId', 'entityId', 'departmentId']);

type Change = { field: string; from: string | null; to: string | null };

function parseChanges(dataJson: string | null | undefined): Change[] {
  if (!dataJson) return [];

  try {
    const payload = JSON.parse(dataJson) as { changes?: Change[] };
    return payload.changes ?? [];
  } catch {
    return [];
  }
}

function pausedUntil(dataJson: string | null | undefined): string | null {
  if (!dataJson) return null;

  try {
    const payload = JSON.parse(dataJson) as { until?: string };
    return payload.until ?? null;
  } catch {
    return null;
  }
}

/**
 * The responsibility's own audit trail. The rules decide what the ledger will ever contain, so
 * "who changed this weekly rule to yearly, and when" gets the same visibility a work item's
 * history has always had.
 */
export function ResponsibilityHistoryModal({
  responsibility,
  onClose,
}: {
  responsibility: ResponsibilityDto | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();

  const events = useQuery({
    queryKey: keys.responsibilities.events(responsibility?.id ?? null),
    queryFn: () => api.responsibilities.events(responsibility!.id),
    enabled: responsibility !== null,
  });

  const describe = (change: Change): string => {
    const label = t(FIELD_LABELS[change.field] ?? change.field);
    if (ID_FIELDS.has(change.field)) return label;

    const value = (raw: string | null): string => {
      if (raw === null || raw === '') return t('common.none');
      if (change.field === 'recurrenceKind') return t(`recurrence.${raw}`);
      if (change.field === 'startDate') return formatDate(raw);
      if (raw === 'true') return t('common.yes');
      if (raw === 'false') return t('common.no');
      return raw;
    };

    return `${label}: ${value(change.from)} → ${value(change.to)}`;
  };

  return (
    <Modal
      opened={responsibility !== null}
      onClose={onClose}
      title={
        <Text fw={600} size="sm">
          {t('respHistory.title', { title: responsibility?.title ?? '' })}
        </Text>
      }
      centered
      size="lg"
    >
      {events.isLoading && <Loader size="sm" />}

      {events.data && events.data.length === 0 && (
        <Text size="sm" c="dimmed">
          {t('respHistory.empty')}
        </Text>
      )}

      {events.data && events.data.length > 0 && (
        <Timeline bulletSize={22} lineWidth={2}>
          {events.data.map((event) => {
            const glyph = EVENT_GLYPH[event.eventType];
            const EventIcon = glyph.icon;
            const changes = parseChanges(event.dataJson);
            const until = event.eventType === 'Paused' ? pausedUntil(event.dataJson) : null;

            return (
              <Timeline.Item
                key={event.id}
                color={glyph.colour}
                bullet={<EventIcon size={12} />}
                title={t(`respEvent.${event.eventType}`)}
              >
                <Text size="xs" c="dimmed">
                  {formatDateTime(event.timestamp)} · {event.userDisplayName}
                </Text>

                {until && (
                  <Text size="xs" c="dimmed">
                    {t('respHistory.pausedUntil', { date: formatDate(until) })}
                  </Text>
                )}

                {changes.map((change) => (
                  <Text key={change.field} size="xs" c="dimmed">
                    {describe(change)}
                  </Text>
                ))}
              </Timeline.Item>
            );
          })}
        </Timeline>
      )}
    </Modal>
  );
}
