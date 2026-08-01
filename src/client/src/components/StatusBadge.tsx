import { Badge, Group, Tooltip } from '@mantine/core';
import { IconAlertTriangle, IconPlayerPause } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { HoldReason, WorkItemStatus } from '../api/types';
import { STATUS_COLOR, STATUS_ICON } from '../theme';

type Props = {
  status: WorkItemStatus;
  isOverdue?: boolean;
  holdReason?: HoldReason | null;
  holdReasonText?: string | null;
  size?: 'xs' | 'sm' | 'md';
};

/**
 * Status is never rendered from a raw enum name: every label goes through the resource files, which
 * is what makes the whole UI switch language without a pass over the screens.
 */
export function StatusBadge({ status, isOverdue, holdReason, holdReasonText, size = 'sm' }: Props) {
  const { t } = useTranslation();
  const Icon = STATUS_ICON[status];

  // The glyphs shrink with the badge; at xs a 14px icon crowds the label out.
  const glyph = size === 'xs' ? 12 : 14;

  return (
    <Group gap={6} wrap="nowrap">
      <Badge color={STATUS_COLOR[status]} variant="light" size={size} leftSection={<Icon size={glyph} />}>
        {t(`status.${status}`)}
      </Badge>

      {isOverdue && status !== 'Missed' && (
        <Badge color="red" variant="outline" size={size} leftSection={<IconAlertTriangle size={glyph} />}>
          {t('status.overdue')}
        </Badge>
      )}

      {holdReason && (
        <Tooltip label={holdReasonText ?? t(`holdReason.${holdReason}`)} disabled={!holdReasonText}>
          <Badge color="orange" variant="outline" size={size} leftSection={<IconPlayerPause size={glyph} />}>
            {t(`holdReason.${holdReason}`)}
          </Badge>
        </Tooltip>
      )}
    </Group>
  );
}
