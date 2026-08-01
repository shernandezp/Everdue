import { MantineProvider } from '@mantine/core';
import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';
import { ChecklistProgress } from './ChecklistProgress';
import { ChecklistTemplateEditor } from '../responsibilities/ChecklistTemplateEditor';
import { CompletionRulesFields } from '../responsibilities/CompletionRulesFields';

function renderIn(node: ReactNode) {
  const wrapper = ({ children }: { children: ReactNode }) => <MantineProvider>{children}</MantineProvider>;
  return render(<>{node}</>, { wrapper });
}

describe('ChecklistProgress', () => {
  it('shows nothing when the item has no checklist', () => {
    // The server sends nulls rather than zeros for exactly this reason: a badge reading "0/0" on every ordinary
    // task is noise on every row of the board. (Mantine injects its own style tags into the container, so the
    // assertion is on the absence of the badge rather than of everything.)
    renderIn(<ChecklistProgress checked={null} total={null} />);
    expect(screen.queryByText(/\d+\/\d+/)).not.toBeInTheDocument();
  });

  it('shows the pair when there is one', () => {
    renderIn(<ChecklistProgress checked={2} total={5} />);
    expect(screen.getByText('2/5')).toBeInTheDocument();
  });

  it('treats a null checked count as none done rather than as absent', () => {
    renderIn(<ChecklistProgress checked={null} total={3} />);
    expect(screen.getByText('0/3')).toBeInTheDocument();
  });
});

describe('CompletionRulesFields', () => {
  it('cannot require a checklist that has no steps', () => {
    renderIn(
      <CompletionRulesFields
        requireChecklist={false}
        requireAttachment={false}
        hasChecklistItems={false}
        onChange={() => {}}
      />,
    );

    // The switch is disabled and says why, rather than letting somebody set a rule the server would then have
    // nothing to enforce.
    expect(screen.getByLabelText(/Require the checklist/)).toBeDisabled();
    expect(screen.getByText(/Add at least one checklist step first/)).toBeInTheDocument();
  });

  it('says the rules are not retroactive once one is on', () => {
    renderIn(
      <CompletionRulesFields
        requireChecklist={false}
        requireAttachment
        hasChecklistItems
        onChange={() => {}}
      />,
    );

    // The first question an administrator asks, answered on the form rather than discovered.
    expect(screen.getByText(/Nothing already completed is reopened/)).toBeInTheDocument();
  });
});

describe('ChecklistTemplateEditor', () => {
  it('says editing the template leaves existing occurrences alone', () => {
    renderIn(<ChecklistTemplateEditor items={[]} onChange={() => {}} max={50} />);

    expect(screen.getByText(/never changes occurrences that already exist/)).toBeInTheDocument();
  });

  it('stops offering to add once the cap is reached', () => {
    const items = Array.from({ length: 3 }, (_, index) => ({ text: `Step ${index}`, required: false }));

    renderIn(<ChecklistTemplateEditor items={items} onChange={() => {}} max={3} />);

    expect(screen.getByRole('button', { name: /Add a step/ })).toBeDisabled();
    expect(screen.getByText('Maximum 3 steps.')).toBeInTheDocument();
  });
});
