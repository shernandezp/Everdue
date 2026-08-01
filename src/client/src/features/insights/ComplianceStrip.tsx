import { Badge, Group, Tooltip } from '@mantine/core';
import { useTranslation } from 'react-i18next';
import type { StripPoint } from '../../api/types';
import { STATUS_COLOR } from '../../theme';

const MARK: Record<string, string> = {
  Completed: '✅',
  CompletedLate: '⏱',
  Missed: '❌',
  OnHold: '⏸',
  Open: '·',
  InProgress: '·',
  Cancelled: '·',
};

/**
 * "Week 29 ✅ / Week 30 ❌ / Week 31 ⏸" — the series the whole product is named after, as chips.
 *
 * A rate says 87%; this says which weeks, which is what a conversation about it actually needs. Colours
 * come from the same map the badges and board columns use, so a status looks the same everywhere, and
 * a chip opens the occurrence behind it — the point of naming a period is being able to go and look.
 */
export function ComplianceStrip({ points, onOpen }: { points: StripPoint[]; onOpen?: (workItemId: string) => void }) {
  const { t } = useTranslation();

  return (
    <Group gap={6} wrap="wrap">
      {points.map((point) => (
        <Tooltip
          key={point.workItemId}
          withArrow
          label={
            `${point.label} · ${t(`status.${point.status}`)}` +
            (point.holdReason ? ` · ${t(`holdReason.${point.holdReason}`)}` : '') +
            (point.periodConcluded ? '' : ` · ${t('insights.inFlight')}`)
          }
        >
          <Badge
            color={STATUS_COLOR[point.status]}
            variant={point.periodConcluded ? 'light' : 'outline'}
            size="lg"
            radius="sm"
            className={onOpen ? 'everdue-chip' : undefined}
            style={onOpen ? { cursor: 'pointer' } : undefined}
            onClick={onOpen ? () => onOpen(point.workItemId) : undefined}
          >
            {MARK[point.status] ?? '·'} {point.label}
          </Badge>
        </Tooltip>
      ))}
    </Group>
  );
}
