import { Badge, Button, Group, ScrollArea, Stack, Text, UnstyledButton } from '@mantine/core';
import {
  IconAlertTriangle,
  IconAt,
  IconBell,
  IconCalendarDue,
  IconChecks,
  IconPlayerPause,
  IconUserPlus,
  type TablerIcon,
} from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { NotificationDto } from '../../api/types';
import { EM_DASH, formatDateTime } from '../../lib/format';

const COLORS: Record<string, string> = {
  Assigned: 'blue',
  DueToday: 'indigo',
  Missed: 'red',
  Mentioned: 'grape',
  PutOnHold: 'orange',
};

/** The same five kinds, as glyphs: the row is scannable before any of it is read. */
const ICONS: Record<string, TablerIcon> = {
  Assigned: IconUserPlus,
  DueToday: IconCalendarDue,
  Missed: IconAlertTriangle,
  Mentioned: IconAt,
  PutOnHold: IconPlayerPause,
};

/**
 * The list itself, with no popover around it. Separated from the bell so what it renders can be
 * asserted without dragging a floating-position library into a test that is about text.
 *
 * Notifications carry render parameters, never rendered text — so the same row a colleague reads in
 * Spanish shows here in whatever language this person chose.
 */
export function NotificationList({
  items,
  unread,
  onOpen,
  onMarkAllRead,
  markingRead,
}: {
  items: NotificationDto[];
  unread: number;
  onOpen: (notification: NotificationDto) => void;
  onMarkAllRead: () => void;
  markingRead: boolean;
}) {
  const { t } = useTranslation();

  return (
    <>
      <Group justify="space-between" mb="xs" px="xs">
        <Text fw={600} size="sm">
          {t('notifications.title')}
        </Text>

        {unread > 0 && (
          <Button
            variant="subtle"
            size="compact-xs"
            leftSection={<IconChecks size={14} />}
            onClick={onMarkAllRead}
            loading={markingRead}
          >
            {t('notifications.markAllRead')}
          </Button>
        )}
      </Group>

      <ScrollArea.Autosize mah={380}>
        <Stack gap={4}>
          {items.map((notification) => {
            const data = notification.data ?? {};
            const colour = COLORS[notification.type] ?? 'gray';
            const Icon = ICONS[notification.type] ?? IconBell;

            return (
              <UnstyledButton
                key={notification.id}
                onClick={() => onOpen(notification)}
                p="xs"
                className="everdue-row"
                style={{
                  // Unread rows keep a tinted background and gain a stripe in their own colour.
                  background: notification.readAt ? undefined : `var(--mantine-color-${colour}-light)`,
                  borderLeft: notification.readAt
                    ? '3px solid transparent'
                    : `3px solid var(--mantine-color-${colour}-filled)`,
                }}
              >
                <Group gap="xs" wrap="nowrap" align="flex-start">
                  <Badge size="xs" variant="light" color={colour} leftSection={<Icon size={11} />}>
                    {t(`notifications.type.${notification.type}`)}
                  </Badge>

                  <Stack gap={2} style={{ flex: 1, minWidth: 0 }}>
                    <Text size="sm" lineClamp={2}>
                      {t(`notifications.message.${notification.type}`, {
                        title: data.title ?? EM_DASH,
                        actor: data.actor ?? t('notifications.someone'),
                        entity: data.entity ?? '',
                      })}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {formatDateTime(notification.createdAt)}
                    </Text>
                  </Stack>
                </Group>
              </UnstyledButton>
            );
          })}

          {items.length === 0 && (
            <Text size="sm" c="dimmed" px="xs" py="sm">
              {t('notifications.empty')}
            </Text>
          )}
        </Stack>
      </ScrollArea.Autosize>
    </>
  );
}
