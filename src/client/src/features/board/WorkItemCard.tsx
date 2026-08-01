import { ActionIcon, Card, Group, Menu, Stack, Text } from '@mantine/core';
import {
  IconArrowBackUp,
  IconCircleCheck,
  IconDotsVertical,
  IconGripVertical,
  IconInfoCircle,
  IconPlayerPause,
  IconPlayerPlay,
} from '@tabler/icons-react';
import { useDraggable } from '@dnd-kit/core';
import type { CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import type { WorkItem } from '../../api/types';
import { StatusBadge } from '../../components/StatusBadge';
import { ChecklistProgress } from '../workitems/ChecklistProgress';
import { formatDueDate } from '../../lib/format';
import { STATUS_COLOR } from '../../theme';

type Props = {
  item: WorkItem;
  onOpen: () => void;
  onStart: () => void;
  onComplete: () => void;
  onHold: () => void;
  onReopen: () => void;
};

export function WorkItemCard({ item, onOpen, onStart, onComplete, onHold, onReopen }: Props) {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: item.id });

  const canStart = item.status === 'Open' || item.status === 'OnHold';
  const canComplete =
    item.status === 'Open' || item.status === 'InProgress' || item.status === 'OnHold' || item.status === 'Missed';
  const canHold = item.status === 'Open' || item.status === 'InProgress';
  const canReopen =
    item.status === 'InProgress' ||
    item.status === 'OnHold' ||
    item.status === 'Completed' ||
    item.status === 'CompletedLate';

  return (
    <Card
      ref={setNodeRef}
      withBorder
      padding="xs"
      className="everdue-interactive"
      style={
        {
          transform: transform ? `translate3d(${transform.x}px, ${transform.y}px, 0)` : undefined,
          opacity: isDragging ? 0.5 : 1,
          cursor: 'pointer',
          // See app.css: eased transform and per-frame dragging do not mix.
          transition: isDragging ? 'none' : undefined,
          // What the card lifts towards on hover: its own status colour, not a generic grey.
          '--everdue-accent': `var(--mantine-color-${STATUS_COLOR[item.status]}-6)`,
        } as CSSProperties
      }
      onClick={onOpen}
    >
      <Stack gap={6}>
        <Group justify="space-between" wrap="nowrap" align="flex-start" gap={4}>
          <Text size="sm" fw={500} lineClamp={3}>
            {item.title}
          </Text>

          <Group gap={0} wrap="nowrap">
            {/* An explicit menu next to the drag handle: dragging is awkward on a phone, and the
                phone IS the mobile experience in v1. */}
            <Menu withinPortal position="bottom-end">
              <Menu.Target>
                <ActionIcon variant="subtle" color="gray" onClick={(event) => event.stopPropagation()}>
                  <IconDotsVertical size={16} />
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown onClick={(event) => event.stopPropagation()}>
                {canStart && (
                  <Menu.Item color="indigo" leftSection={<IconPlayerPlay size={16} />} onClick={onStart}>
                    {t('workItem.start')}
                  </Menu.Item>
                )}
                {canComplete && (
                  <Menu.Item color="teal" leftSection={<IconCircleCheck size={16} />} onClick={onComplete}>
                    {item.status === 'Missed' ? t('workItem.completeLate') : t('workItem.complete')}
                  </Menu.Item>
                )}
                {canHold && (
                  <Menu.Item color="orange" leftSection={<IconPlayerPause size={16} />} onClick={onHold}>
                    {t('workItem.hold')}
                  </Menu.Item>
                )}
                {canReopen && (
                  <Menu.Item color="blue" leftSection={<IconArrowBackUp size={16} />} onClick={onReopen}>
                    {t('workItem.reopen')}
                  </Menu.Item>
                )}
                <Menu.Divider />
                <Menu.Item leftSection={<IconInfoCircle size={16} />} onClick={onOpen}>
                  {t('workItem.detail')}
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>

            <ActionIcon
              variant="subtle"
              color="gray"
              {...listeners}
              {...attributes}
              onClick={(event) => event.stopPropagation()}
              style={{ cursor: 'grab', touchAction: 'none' }}
            >
              <IconGripVertical size={16} />
            </ActionIcon>
          </Group>
        </Group>

        {item.entityName && (
          <Text size="xs" c="dimmed" lineClamp={1}>
            {item.entityName}
          </Text>
        )}

        <Group justify="space-between" gap={4} wrap="nowrap">
          <Group gap={4} wrap="nowrap">
            <StatusBadge
              status={item.status}
              isOverdue={item.isOverdue}
              holdReason={item.holdReason}
              holdReasonText={item.holdReasonText}
              size="xs"
            />
            <ChecklistProgress checked={item.checklistChecked} total={item.checklistTotal} />
          </Group>
          <Text size="xs" c={item.isOverdue ? 'red' : 'dimmed'} style={{ whiteSpace: 'nowrap' }}>
            {formatDueDate(item.dueDate, item.responsibilityId !== null)}
          </Text>
        </Group>
      </Stack>
    </Card>
  );
}
