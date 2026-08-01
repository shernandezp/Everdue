import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { DemoStatus } from '../../api/types';

const status = vi.fn<() => Promise<DemoStatus>>();
const set = vi.fn();

vi.mock('../../lib/api', () => ({ api: { demo: { status: () => status(), set: (body: unknown) => set(body) } } }));
vi.mock('../../lib/notify', () => ({ notifySaved: vi.fn(), notifyError: vi.fn() }));
vi.mock('../auth/session', () => ({ useSession: () => ({ refresh: vi.fn() }) }));

const { DemoModeCard } = await import('./DemoModeCard');

const WORKSPACE = 'Acme Logistics';

function aStatus(overrides: Partial<DemoStatus> = {}): DemoStatus {
  return {
    enabled: false,
    resetAllowed: true,
    confirmationPhrase: WORKSPACE,
    demoPassword: 'EverdueDemo2026!',
    ...overrides,
  } as DemoStatus;
}

function renderCard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  render(
    <QueryClientProvider client={client}>
      <MantineProvider>
        <DemoModeCard />
      </MantineProvider>
    </QueryClientProvider>,
  );
}

// Queried by text and walked up to the button, not getByRole({ name }).
//
// This is a jsdom defect, not a preference, and not something this file can style its way out of: any Mantine
// size prop compiles to a length like `calc(35rem * var(--mantine-scale))`, and `--mantine-scale` is only
// defined by @mantine/core/styles.css, which the tests do not load. jsdom 30 then resolves that length to an
// unresolved `var()`, and font-sizes.js destructures `FONT_SIZE_REGEXP.exec(...)` without a null check —
// "object null is not iterable". getByRole reaches it through dom-accessibility-api's visibility walk.
//
// Verified rather than assumed: defining --mantine-scale on :root, both inline and as a real stylesheet rule,
// does not help — jsdom does not resolve custom properties through the cascade on that path. The fix belongs
// upstream. See findings.md.
const button = (label: string) => screen.getByText(label).closest('button')!;
const findButton = async (label: string) => (await screen.findByText(label)).closest('button')!;

const confirmButton = () => button('Delete everything and continue');

/** Opens the confirmation and waits for it: Mantine transitions the modal in, so it is not there on the next tick. */
async function openTheDialog(trigger = 'Turn on demo mode') {
  const user = userEvent.setup();

  await user.click(await findButton(trigger));
  await screen.findByLabelText('Workspace name');

  return user;
}

describe('DemoModeCard', () => {
  beforeEach(() => {
    status.mockReset().mockResolvedValue(aStatus());
    set.mockReset().mockResolvedValue({
      status: aStatus({ enabled: true }),
      deleted: { workItems: 3, responsibilities: 1, entities: 1, departments: 1, users: 1, attachments: 0, notifications: 0 },
      seeded: { users: 6, entities: 11, responsibilities: 12, occurrences: 400, tasks: 15, password: 'EverdueDemo2026!' },
    });
  });

  /**
   * The whole safety story is that this cannot be fired by accident. Both boxes must be right, and the
   * workspace name is compared exactly — "close enough" is not a confirmation, which is also what the
   * server does with it.
   */
  it('keeps the confirm button disabled until the workspace name and a password are both given', async () => {
    renderCard();
    const user = await openTheDialog();

    expect(confirmButton()).toBeDisabled();

    await user.type(screen.getByLabelText('Workspace name'), WORKSPACE);
    expect(confirmButton()).toBeDisabled();

    await user.type(screen.getByLabelText('Your password'), 'hunter2');
    expect(confirmButton()).toBeEnabled();
  });

  it('refuses a workspace name that differs only in case', async () => {
    renderCard();
    const user = await openTheDialog();

    await user.type(screen.getByLabelText('Workspace name'), WORKSPACE.toLowerCase());
    await user.type(screen.getByLabelText('Your password'), 'hunter2');

    expect(confirmButton()).toBeDisabled();
    expect(set).not.toHaveBeenCalled();
  });

  it('sends both confirmations when it does fire', async () => {
    renderCard();
    const user = await openTheDialog();

    await user.type(screen.getByLabelText('Workspace name'), WORKSPACE);
    await user.type(screen.getByLabelText('Your password'), 'hunter2');
    await user.click(confirmButton());

    await waitFor(() =>
      expect(set).toHaveBeenCalledWith({ enabled: true, confirmation: WORKSPACE, password: 'hunter2' }),
    );
  });

  /**
   * Demo:AllowReset is off. Nothing is rendered at all — not a disabled button, which would only send
   * somebody looking for the setting that would enable it.
   */
  it('renders nothing when the install does not allow a reset', async () => {
    status.mockResolvedValue(aStatus({ resetAllowed: false }));
    renderCard();

    await waitFor(() => expect(status).toHaveBeenCalled());
    expect(screen.queryByText('Demo mode')).not.toBeInTheDocument();
  });

  /** Turning it off is the same destructive operation, so it asks for exactly the same two things. */
  it('offers to clear everything when demo mode is already on', async () => {
    status.mockResolvedValue(aStatus({ enabled: true }));
    renderCard();

    await openTheDialog('Turn off and clear everything');

    expect(screen.getByLabelText('Your password')).toBeInTheDocument();
    expect(confirmButton()).toBeDisabled();
  });
});
