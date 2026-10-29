// @vitest-environment jsdom
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RejectDialog } from './RejectDialog';

/* RejectDialog stands in for the dialogs that reset their form during render: a cancelled draft
   must not come back.

   Not covered: focus doesn't return to the trigger on close. That was already the case before and
   is worth looking into separately.

   i18next is stubbed; `t` returns the key. */
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
