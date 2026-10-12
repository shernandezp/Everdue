import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

type Row = Record<string, unknown>;

const departments = [
  { id: 'd1', name: 'Operations', active: true },
  { id: 'd2', name: 'Finance', active: true },
];

vi.mock('../../lib/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../lib/api')>()),
  api: {
    departments: {
      list: () => Promise.resolve({ items: departments, totalCount: 2, page: 1, pageSize: 25 }),
    },
  },
}));
vi.mock('../../lib/notify', () => ({ notifySaved: vi.fn(), notifyError: vi.fn() }));

// jsdom renders mantine-datatable without its rows.
vi.mock('mantine-datatable', () => ({
  DataTable: ({ records, columns }: { records: Row[]; columns: { accessor: string; render?: (row: Row) => ReactNode }[] }) => (
    <table>
      <tbody>
        {records.map((row) => (
          <tr key={String(row.id)}>
            {columns.map((column) => (
              <td key={column.accessor}>{column.render ? column.render(row) : String(row[column.accessor])}</td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  ),
}));

const { DepartmentsPage } = await import('./DepartmentsPage');

describe('DepartmentsPage', () => {
  it('opens the edit form with the values of the row being edited, not the previous one', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MantineProvider>
          <MemoryRouter>
            <DepartmentsPage />
          </MemoryRouter>
        </MantineProvider>
      </QueryClientProvider>,
    );

    const user = userEvent.setup();
    const edit = await screen.findAllByLabelText('Edit');

    await user.click(edit[0]);
    expect(await screen.findByDisplayValue('Operations')).toBeInTheDocument();

    await user.click(screen.getByText('Cancel').closest('button')!);
    await waitFor(() => expect(screen.queryByDisplayValue('Operations')).not.toBeInTheDocument());

    await user.click(screen.getAllByLabelText('Edit')[1]);
    expect(await screen.findByDisplayValue('Finance')).toBeInTheDocument();
  });
});
