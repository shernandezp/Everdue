import { MantineProvider } from '@mantine/core';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { NotificationDto } from '../../api/types';
import { NotificationList } from './NotificationList';

function notification(overrides: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    type: 'Missed',
    workItemId: '22222222-2222-2222-2222-222222222222',
    commentId: null,
    data: { title: 'Inventory check' },
    createdAt: '2026-07-29T12:00:00Z',
    readAt: null,
    ...overrides,
  } as NotificationDto;
}

function renderList(props: Partial<Parameters<typeof NotificationList>[0]> = {}) {
  const defaults = {
    items: [notification()],
    unread: 1,
    onOpen: vi.fn(),
    onMarkAllRead: vi.fn(),
    markingRead: false,
  };

  const merged = { ...defaults, ...props };

  render(
    <MantineProvider>
      <NotificationList {...merged} />
    </MantineProvider>,
  );

  return merged;
}

describe('NotificationList', () => {
  /**
   * The payload is parameters, not rendered text: the message is assembled here, in the reader's own
   * language, from the same facts a colleague reads in another.
   */
  it('assembles the message from the notification parameters', () => {
    renderList();

    expect(screen.getByText(/Inventory check/)).toBeInTheDocument();
  });

  it('falls back when the actor is unknown rather than printing a placeholder', () => {
    renderList({ items: [notification({ type: 'Assigned', data: { title: 'Call the supplier' } })] });

    expect(screen.getByText(/Call the supplier/)).toBeInTheDocument();
    expect(screen.queryByText(/undefined/)).not.toBeInTheDocument();
  });

  it('opens the item that was clicked', async () => {
    const props = renderList();

    await userEvent.click(screen.getByText(/Inventory check/));

    expect(props.onOpen).toHaveBeenCalledWith(expect.objectContaining({ id: notification().id }));
  });

  it('offers "mark all read" only when something is unread', async () => {
    const props = renderList();

    await userEvent.click(screen.getByRole('button', { name: /mark all read/i }));
    expect(props.onMarkAllRead).toHaveBeenCalled();

    renderList({ unread: 0 });
    expect(screen.getAllByRole('button', { name: /mark all read/i })).toHaveLength(1);
  });

  it('says so when there is nothing', () => {
    renderList({ items: [], unread: 0 });

    expect(screen.getByText(/nothing yet/i)).toBeInTheDocument();
  });
});
