import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const entityOptions = vi.fn();
const entityGet = vi.fn();
const departmentOptions = vi.fn();
const departmentGet = vi.fn();

vi.mock('../lib/api', () => ({
  api: {
    entities: { options: () => entityOptions(), get: (id: string) => entityGet(id) },
    departments: { options: () => departmentOptions(), get: (id: string) => departmentGet(id) },
  },
}));

const { DepartmentPicker, EntityPicker } = await import('./pickers');

function renderWithProviders(ui: ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <MantineProvider>{ui}</MantineProvider>
    </QueryClientProvider>,
  );
}

describe('pickers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    entityOptions.mockResolvedValue([{ id: 'e1', name: 'Acme', type: 'Customer' }]);
    departmentOptions.mockResolvedValue([{ id: 'd1', name: 'Operations' }]);
  });

  it('shows an inactive entity that is already selected', async () => {
    entityGet.mockResolvedValue({ id: 'e2', name: 'Old Client', type: 'Customer', active: false, customFields: [] });

    renderWithProviders(<EntityPicker value="e2" onChange={vi.fn()} />);

    expect(await screen.findByDisplayValue('Old Client · Customer')).toBeInTheDocument();
    expect(entityGet).toHaveBeenCalledWith('e2');
  });

  it('shows an inactive department that is already selected', async () => {
    departmentGet.mockResolvedValue({ id: 'd2', name: 'Old Team', active: false });

    renderWithProviders(<DepartmentPicker value="d2" onChange={vi.fn()} />);

    expect(await screen.findByDisplayValue('Old Team')).toBeInTheDocument();
    expect(departmentGet).toHaveBeenCalledWith('d2');
  });

  it('does not look up a department that is already in the options', async () => {
    renderWithProviders(<DepartmentPicker value="d1" onChange={vi.fn()} />);

    expect(await screen.findByDisplayValue('Operations')).toBeInTheDocument();
    expect(departmentGet).not.toHaveBeenCalled();
  });
});
