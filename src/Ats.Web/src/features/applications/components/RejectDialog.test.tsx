// @vitest-environment jsdom
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RejectDialog } from './RejectDialog';

/* The first component test in the codebase, and it exists for a reason rather than for coverage:
   nine dialogs moved their "clear the form when it opens" from an effect to a render-phase
   adjustment. The rule they satisfy is a lint rule, but the behaviour they must not lose — a draft
   from a cancelled attempt never reappearing — is invisible to every pure-logic test.

   RejectDialog stands in for all nine. They share one shape, and a test per dialog would be eight
   copies asserting the same mechanism.

   jsdom is opted into per file with the docblock above, so the pure-logic suites keep the faster
   node environment.

   Not asserted here: that closing returns focus to the trigger. It does not — focus lands on
   <body> — but it behaves identically before and after this change, so a test would guard nothing
   here. Radix does trap focus correctly on open, so this is worth looking into on its own rather
   than dismissing as a jsdom artefact.

   i18next is stubbed: the assertions are about state, and a missing-translation warning would be
   noise. `t` returning the key makes the queries readable. */
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

afterEach(() => {
  vi.clearAllMocks();
});

/* Mirrors how the application detail page drives the dialog: a button owns the open state, which
   means the trigger is a real element focus can return to. */
function Harness({ onConfirm = () => {} }: { onConfirm?: (reason: string) => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        open dialog
      </button>
      <RejectDialog open={open} onOpenChange={setOpen} onConfirm={onConfirm} submitting={false} />
    </>
  );
}

const trigger = () => screen.getByRole('button', { name: 'open dialog' });
const reasonBox = () => screen.getByRole('textbox');

describe('RejectDialog', () => {
  it('renders nothing until it is opened', () => {
    render(<Harness />);

    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('starts empty on a second open after a draft was abandoned', async () => {
    // The property the old effect existed for. It now falls out of unmounting, so it has to be
    // pinned somewhere or a future refactor could quietly reintroduce the lingering draft.
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(trigger());
    await user.type(reasonBox(), 'half-written reason');
    expect(reasonBox()).toHaveValue('half-written reason');

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).toBeNull();

    await user.click(trigger());
    expect(reasonBox()).toHaveValue('');
  });

  it('clears a validation error between opens too', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(trigger());
    await user.click(screen.getByRole('button', { name: 'applicationDetail.reject_modal.confirm' }));
    expect(screen.getByText('applicationDetail.reject_modal.reasonRequired')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await user.click(trigger());

    expect(screen.queryByText('applicationDetail.reject_modal.reasonRequired')).toBeNull();
  });

  it('does not confirm with an empty reason', async () => {
    const onConfirm = vi.fn();
    const user = userEvent.setup();
    render(<Harness onConfirm={onConfirm} />);

    await user.click(trigger());
    await user.click(screen.getByRole('button', { name: 'applicationDetail.reject_modal.confirm' }));

    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('confirms with the trimmed reason', async () => {
    const onConfirm = vi.fn();
    const user = userEvent.setup();
    render(<Harness onConfirm={onConfirm} />);

    await user.click(trigger());
    await user.type(reasonBox(), '  not enough experience  ');
    await user.click(screen.getByRole('button', { name: 'applicationDetail.reject_modal.confirm' }));

    expect(onConfirm).toHaveBeenCalledWith('not enough experience');
  });
});
