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

const detailsInput = () => screen.getAllByLabelText(/Details/)[0];

describe('HoldDialog', () => {
  it('confirms with a single tap on a fixed reason — two taps counting the menu', async () => {
    const { onConfirm } = renderDialog();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Waiting on customer' }));

    expect(onConfirm).toHaveBeenCalledWith('WaitingCustomer', null);
  });

  it('offers no way to confirm without a reason', () => {
    renderDialog();

    // No generic Confirm button exists until "Other" is opened; the reasons are the buttons.
    expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument();
  });

  it('requires free text when the reason is Other — the one reason that explains nothing on its own', async () => {
    const { onConfirm } = renderDialog();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Other' }));
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(await screen.findByText('Describe the reason')).toBeInTheDocument();

    await user.type(detailsInput(), 'Waiting on the landlord');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).toHaveBeenCalledWith('Other', 'Waiting on the landlord');
  });

  it('sends a note typed before the tap along with the reason', async () => {
    const { onConfirm } = renderDialog();
    const user = userEvent.setup();

    await user.type(detailsInput(), 'Second chase this week');
    await user.click(screen.getByRole('button', { name: 'Waiting for approval' }));

    expect(onConfirm).toHaveBeenCalledWith('WaitingApproval', 'Second chase this week');
  });

  it('trims the free text and drops it entirely when it is blank', async () => {
    const { onConfirm } = renderDialog();
    const user = userEvent.setup();

    await user.type(detailsInput(), '   ');
    await user.click(screen.getByRole('button', { name: 'Waiting for approval' }));

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
