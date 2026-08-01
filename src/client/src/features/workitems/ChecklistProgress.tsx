import { Badge } from '@mantine/core';
import { IconListCheck } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';

/**
 * `4/7` beside a card or a row.
 *
 * Renders nothing when the item has no checklist — the server sends nulls rather than zeros for exactly this
 * reason, so a badge is absent instead of reading "0/0" on every ordinary task.
 */
export function ChecklistProgress({
  checked,
  total,
  size = 'xs',
}: {
  checked?: number | null;
  total?: number | null;
  size?: 'xs' | 'sm';
}) {
  const { t } = useTranslation();

  if (!total) return null;

  const done = checked ?? 0;
  const complete = done >= total;

  return (
    <Badge
      size={size}
      variant="light"
      color={complete ? 'teal' : 'gray'}
      leftSection={<IconListCheck size={12} />}
      title={t('checklist.progressTitle', { checked: done, total })}
    >
      {done}/{total}
    </Badge>
  );
}
