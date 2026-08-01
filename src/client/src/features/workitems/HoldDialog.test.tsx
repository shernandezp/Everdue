import { MantineProvider } from '@mantine/core';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { HoldDialog } from './HoldDialog';

function renderDialog(onConfirm = vi.fn(), onClose = vi.fn()) {
  const wrapper = ({ children }: { children: ReactNode }) => <MantineProvider>{children}</MantineProvider>;
  render(<HoldDialog opened onClose={onClose} onConfirm={onConfirm} />, { wrapper });
  return { onConfirm, onClose };
}

// Mantine's Select renders a visible input plus a hidden one for form submission, so both carry the
// label — take the one the user actually clicks.
const reasonInput = () => screen.getAllByLabelText('Reason')[0];
const detailsInput = () => screen.getAllByLabelText(/Details/)[0];

async function chooseReason(label: string) {
  const user = userEvent.setup();
  await user.click(reasonInput());
  await user.click(await screen.findByText(label));
  return user;
}

describe('HoldDialog', () => {
  it('refuses to put anything on hold without a reason', async () => {
    const { onConfirm } = renderDialog();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(await screen.findByText('Choose a reason')).toBeInTheDocument();
  });

  it('confirms with a reason from the fixed taxonomy and no free text', async () => {
    const { onConfirm } = renderDialog();
    const user = await chooseReason('Waiting on customer');

    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).toHaveBeenCalledWith('WaitingCustomer', null);
  });

  it('requires free text when the reason is Other — the one reason that explains nothing on its own', async () => {
    const { onConfirm } = renderDialog();
    const user = await chooseReason('Other');

    await user.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(onConfirm).not.toHaveBeenCalled();
    expect(await screen.findByText('Describe the reason')).toBeInTheDocument();

    await user.type(detailsInput(), 'Waiting on the landlord');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).toHaveBeenCalledWith('Other', 'Waiting on the landlord');
  });

  it('trims the free text and drops it entirely when it is blank', async () => {
    const { onConfirm } = renderDialog();
    const user = await chooseReason('Waiting for approval');

    await user.type(detailsInput(), '   ');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).toHaveBeenCalledWith('WaitingApproval', null);
  });

  it('closes without confirming when cancelled', async () => {
    const { onConfirm, onClose } = renderDialog();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });
});
