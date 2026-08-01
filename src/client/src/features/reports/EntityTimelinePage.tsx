import { Card, Group, Loader, Stack, Text, Timeline } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';
import { StatusBadge } from '../../components/StatusBadge';
import { ChecklistProgress } from '../workitems/ChecklistProgress';
import { api } from '../../lib/api';
import { formatDate, formatDueDate } from '../../lib/format';
import { STATUS_COLOR, STATUS_ICON } from '../../theme';
import { WorkItemDrawer } from '../workitems/WorkItemDrawer';
import { keys } from '../../lib/queryKeys';

/**
 * The customer-service memory: this entity's occurrences interleaved with its one-off work, newest
 * first. "Week 29 done / Week 30 missed / Week 31 waiting on the customer", in one place.
 */
export function EntityTimelinePage() {
  const { t } = useTranslation();
  const { entityId } = useParams<{ entityId: string }>();
  const [openId, setOpenId] = useState<string | null>(null);

  const timeline = useQuery({
    queryKey: keys.reports.timeline(entityId),
    queryFn: () => api.reports.timeline(entityId!),
    enabled: Boolean(entityId),
  });

  return (
    <>
      <PageHeader
        title={timeline.data?.entityName ?? t('reports.timelineTitle')}
        description={
          timeline.data
            ? `${t(`entityType.${timeline.data.entityType}`)} · ${t('reports.lastActivity')}: ${
                timeline.data.lastActivityAt ? formatDate(timeline.data.lastActivityAt) : t('common.never')
              }`
            : undefined
        }
      />

      {timeline.isLoading && <Loader />}

      {timeline.data && timeline.data.items.length === 0 && (
        <Text size="sm" c="dimmed">
          {t('common.noResults')}
        </Text>
      )}

      <Card withBorder padding="lg">
        {/* Bullets carry the status glyph as well as its colour — the same pair the badges use. */}
        <Timeline bulletSize={22} lineWidth={2}>
          {(timeline.data?.items ?? []).map((item) => {
            const StatusIcon = STATUS_ICON[item.status];

            return (
              <Timeline.Item
                key={item.workItemId}
                color={STATUS_COLOR[item.status]}
                bullet={<StatusIcon size={12} />}
                title={
                  <Group gap="xs" wrap="wrap" onClick={() => setOpenId(item.workItemId)} style={{ cursor: 'pointer' }}>
                    <Text size="sm" fw={500}>
                      {item.title}
                    </Text>
                    <StatusBadge
                      status={item.status}
                      holdReason={item.holdReason}
                      holdReasonText={item.holdReasonText}
                      size="xs"
                    />

                    {/* An entity's inspection history is exactly where somebody wants to see how much got ticked. */}
                    <ChecklistProgress checked={item.checklistChecked} total={item.checklistTotal} />
                  </Group>
                }
              >
                <Stack gap={2}>
                  <Text size="xs" c="dimmed">
                    {item.isOccurrence ? t('workItem.occurrence') : t('workItem.oneOff')}
                    {item.responsibilityTitle ? ` · ${item.responsibilityTitle}` : ''}
                  </Text>
                  <Text size="xs" c="dimmed">
                    {t('workItem.dueDate')}: {formatDueDate(item.dueDate, item.isOccurrence)}
                    {item.completedAt ? ` · ${t('workItem.completed')}: ${formatDate(item.completedAt)}` : ''}
                  </Text>
                </Stack>
              </Timeline.Item>
            );
          })}
        </Timeline>
      </Card>

      <WorkItemDrawer id={openId} onClose={() => setOpenId(null)} />
    </>
  );
}
