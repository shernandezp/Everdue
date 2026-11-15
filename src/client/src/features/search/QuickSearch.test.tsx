import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { WorkItem } from '../../api/types';
import { QuickSearch } from './QuickSearch';

vi.mock('../../lib/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../lib/api')>();

  return { ...actual, api: { ...actual.api, workItems: { ...actual.api.workItems, search: vi.fn() } } };
});

vi.mock('../workitems/WorkItemDrawer', () => ({
  WorkItemDrawer: ({ id }: { id: string | null }) => (id ? <div>drawer for {id}</div> : null),
}));

const { api } = await import('../../lib/api');
const search = vi.mocked(api.workItems.search);

const hit = (id: string, title: string, entityName: string | null = null) =>
  ({ id, title, entityName, ownerDisplayName: 'Ana', status: 'Open', dueDate: '2026-11-11T04:59:59Z', isOverdue: false }) as WorkItem;

async function open() {
  render(
    <MantineProvider>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <QuickSearch />
        </MemoryRouter>
      </QueryClientProvider>
    </MantineProvider>,
  );

  await userEvent.click(screen.getAllByRole('button', { name: 'Search tasks' })[0]);
  return screen.findByRole('textbox');
}

describe('QuickSearch', () => {
  beforeEach(() => search.mockReset());

  it('marks what matched, accents ignored, and opens the first hit on Enter', async () => {
    search.mockResolvedValue([hit('a1', 'Guía de recepción', 'Ferretería El Progreso'), hit('b2', 'Recepción mensual')]);
    const input = await open();

    await userEvent.type(input, 'RECEPCION ferreteria');

    expect(await screen.findByText('Ferretería', { selector: 'mark' })).toBeInTheDocument();
    expect(screen.getAllByText('recepción', { selector: 'mark' })).toHaveLength(1);
    expect(search).toHaveBeenLastCalledWith('RECEPCION ferreteria', 8);

    await userEvent.keyboard('{Enter}');

    expect(await screen.findByText('drawer for a1')).toBeInTheDocument();
  });

  it('moves with the arrow keys before opening', async () => {
    search.mockResolvedValue([hit('a1', 'Daily cash count'), hit('b2', 'Cash reconciliation')]);
    const input = await open();

    await userEvent.type(input, 'cash');
    await screen.findByText('reconciliation', { exact: false });
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(await screen.findByText('drawer for b2')).toBeInTheDocument();
  });

  it('says so when nothing matches, and does not ask the server about a single letter', async () => {
    search.mockResolvedValue([]);
    const input = await open();

    await userEvent.type(input, 'x');
    expect(screen.getByText(/Type one or more words/)).toBeInTheDocument();
    expect(search).not.toHaveBeenCalled();

    await userEvent.type(input, 'yzzy');
    expect(await screen.findByText('No task matches “xyzzy”.')).toBeInTheDocument();
  });
});
