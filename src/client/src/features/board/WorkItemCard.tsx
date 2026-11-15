import { ActionIcon, Avatar, Badge, Card, Group, Loader, Menu, Stack, Text, Tooltip } from '@mantine/core';
import {
  IconArrowBackUp,
  IconCalendarEvent,
  IconCalendarExclamation,
  IconCircleCheck,
  IconDotsVertical,
  IconGripVertical,
  IconInfoCircle,
  IconPlayerPause,
  IconPlayerPlay,
  IconRepeat,
} from '@tabler/icons-react';
import { useDraggable } from '@dnd-kit/core';
import type { CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import type { WorkItem } from '../../api/types';
import { ChecklistProgress } from '../workitems/ChecklistProgress';
import { formatDueDate } from '../../lib/format';
import { ENTITY_ICON, STATUS_BAND, STATUS_COLOR, STATUS_ICON } from '../../theme';

type Props = {
  item: WorkItem;
  onOpen: () => void;
  onStart: () => void;
  onComplete: () => void;
  onHold: () => void;
  onReopen: () => void;

  /** This card's own action is in flight. The whole card says so — a silent button gets tapped twice. */
  busy?: boolean;
};

export function WorkItemCard({ item, onOpen, onStart, onComplete, onHold, onReopen, busy }: Props) {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: item.id });

  const StatusIcon = STATUS_ICON[item.status];
  const EntityIcon = item.entityType ? ENTITY_ICON[item.entityType] : null;
  const DueIcon = item.isOverdue ? IconCalendarExclamation : IconCalendarEvent;

  const canStart = item.status === 'Open' || item.status === 'OnHold';
  const canComplete =
    item.status === 'Open' || item.status === 'InProgress' || item.status === 'OnHold' || item.status === 'Missed';
  const canHold = item.status === 'Open' || item.status === 'InProgress';
  // CompletedLate is deliberately absent: reopening a late completion would erase a recorded miss,
  // so the server refuses it and the card does not offer it (see boardColumns.resolveDrop).
  const canReopen = item.status === 'InProgress' || item.status === 'OnHold' || item.status === 'Completed';

  return (
    <Card
      ref={setNodeRef}
      withBorder
      padding={0}
      className="everdue-interactive everdue-task"
      style={
        {
          transform: transform ? `translate3d(${transform.x}px, ${transform.y}px, 0)` : undefined,
          opacity: isDragging || busy ? 0.5 : 1,
          cursor: 'pointer',
          pointerEvents: busy ? 'none' : undefined,
          // See app.css: eased transform and per-frame dragging do not mix.
          transition: isDragging ? 'none' : undefined,
          // What the card lifts towards on hover: its own status colour, not a generic grey.
          '--everdue-accent': `var(--mantine-color-${STATUS_COLOR[item.status]}-6)`,
          '--everdue-band': `var(--mantine-color-${STATUS_BAND[item.status][0]})`,
          '--everdue-fade': `var(--mantine-color-${STATUS_BAND[item.status][1]})`,
        } as CSSProperties
      }
      onClick={onOpen}
    >
      {/* The band says what state the task is in, so the body is free for what the task is. */}
      <div className="everdue-task-band">
        <Group gap={6} wrap="nowrap" style={{ minWidth: 0 }}>
          <span className="everdue-task-status">
            <StatusIcon size={14} stroke={2.4} />
          </span>
          <Text size="xs" fw={700} truncate>
            {t(`status.${item.status}`)}
          </Text>
        </Group>

        <Group gap={0} wrap="nowrap">
          {busy && <Loader size="xs" color="white" mr={4} />}

            <Menu withinPortal position="bottom-end">
              <Menu.Target>
                <ActionIcon
                  variant="transparent"
                  color="white"
                  size="sm"
                  aria-label={t('common.actions')}
                  onClick={(event) => event.stopPropagation()}
                >
                  <IconDotsVertical size={16} />
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown onClick={(event) => event.stopPropagation()}>
                {canStart && (
                  <Menu.Item color="violet" leftSection={<IconPlayerPlay size={16} />} onClick={onStart}>
                    {t('workItem.start')}
                  </Menu.Item>
                )}
                {canComplete && (
                  <Menu.Item color="green" leftSection={<IconCircleCheck size={16} />} onClick={onComplete}>
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
              variant="transparent"
              color="white"
              size="sm"
              aria-label={t('workItem.drag')}
              {...listeners}
              {...attributes}
              onClick={(event) => event.stopPropagation()}
              style={{ cursor: 'grab', touchAction: 'none' }}
            >
              <IconGripVertical size={16} />
            </ActionIcon>
        </Group>
      </div>

      <Stack gap={6} p="xs">
        <Text size="sm" fw={600} lh={1.3} lineClamp={3}>
          {item.title}
        </Text>

        {item.entityName && (
          <Group gap={6} wrap="nowrap" c="dimmed">
            {EntityIcon && (
              <Tooltip label={t(`entityType.${item.entityType}`)}>
                <span className="everdue-task-glyph">
                  <EntityIcon size={13} stroke={2} />
                </span>
              </Tooltip>
            )}
            <Text size="xs" truncate>
              {item.entityName}
            </Text>
          </Group>
        )}

        {(item.holdReason || item.checklistTotal) && (
          <Group gap={4}>
            {item.holdReason && (
              <Tooltip label={item.holdReasonText} disabled={!item.holdReasonText}>
                <Badge size="xs" variant="light" color="orange" leftSection={<IconPlayerPause size={11} />}>
                  {t(`holdReason.${item.holdReason}`)}
                </Badge>
              </Tooltip>
            )}
            <ChecklistProgress checked={item.checklistChecked} total={item.checklistTotal} />
          </Group>
        )}

        <Group justify="space-between" wrap="nowrap" gap="xs">
          <Group gap={6} wrap="nowrap" c={item.isOverdue ? 'red' : 'dimmed'}>
            <span className="everdue-task-glyph">
              <DueIcon size={13} stroke={2} />
            </span>
            <Text size="xs" fw={item.isOverdue ? 700 : 500} style={{ whiteSpace: 'nowrap' }}>
              {formatDueDate(item.dueDate, item.responsibilityId != null)}
            </Text>
            {item.responsibilityId != null && (
              <Tooltip label={t('workItem.occurrence')}>
                <IconRepeat size={13} stroke={2} style={{ flexShrink: 0, opacity: 0.7 }} />
              </Tooltip>
            )}
          </Group>

          <Tooltip label={item.ownerDisplayName}>
            <Avatar size={22} radius="xl" color="initials" name={item.ownerDisplayName} />
          </Tooltip>
        </Group>
      </Stack>
    </Card>
  );
}
