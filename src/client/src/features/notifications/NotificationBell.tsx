import { ActionIcon, Indicator, Popover } from '@mantine/core';
import { IconBell, IconBellRinging } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import type { NotificationDto } from '../../api/types';
import { workItemLink } from '../../lib/routes';
import { NotificationList } from './NotificationList';
import { useMarkRead, useNotifications, useUnreadCount } from './hooks';

/**
 * The point of this control: the tool reaches people instead of waiting to be opened.
 * The bell itself only owns the popover and the polling — the list is its own component.
 */
export function NotificationBell() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [opened, setOpened] = useState(false);

  const unread = useUnreadCount();
  const list = useNotifications(opened);
  const markRead = useMarkRead();

  const count = unread.data?.unread ?? 0;

  const open = (notification: NotificationDto) => {
    if (!notification.readAt) markRead.mutate([notification.id]);
    setOpened(false);

    if (notification.workItemId) {
      navigate(workItemLink(notification.workItemId));
    }
  };

  return (
    <Popover opened={opened} onChange={setOpened} position="bottom-end" width={360} withinPortal shadow="md">
      <Popover.Target>
        {/* Unread mail is red and moves; read mail is a quiet grey bell. */}
        <Indicator
          disabled={count === 0}
          label={count > 99 ? '99+' : count}
          size={16}
          offset={4}
          color="red"
          processing
        >
          <ActionIcon
            variant={count > 0 ? 'light' : 'subtle'}
            color={count > 0 ? 'red' : 'gray'}
            size="lg"
            aria-label={t('notifications.title')}
            onClick={() => setOpened((isOpen) => !isOpen)}
          >
            {count > 0 ? <IconBellRinging size={20} /> : <IconBell size={20} />}
          </ActionIcon>
        </Indicator>
      </Popover.Target>

      <Popover.Dropdown p="xs">
        <NotificationList
          items={list.data?.items ?? []}
          unread={count}
          onOpen={open}
          onMarkAllRead={() => markRead.mutate(undefined)}
          markingRead={markRead.isPending}
        />
      </Popover.Dropdown>
    </Popover>
  );
}
