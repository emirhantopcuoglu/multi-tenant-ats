import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Checkbox, Modal } from '@/components/ui';
import { fullName, useUsers } from '@/features/users/useUsers';
import type { ReassignInterviewersRequest } from '@/types/interview';

interface ReassignInterviewersModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Current panel, used to pre-fill the checkboxes. */
  interviewerUserIds: string[];
  submitting: boolean;
  onConfirm: (body: ReassignInterviewersRequest) => void;
}

/* Swaps who is on the panel without touching the schedule — the gap that previously forced a
   recruiter to cancel and rebook (losing the interview's history) just because someone fell ill.

   Sends the whole panel rather than an add/remove delta, matching the endpoint: two recruiters
   editing at once then overwrite each other with a complete intent instead of interleaving into a
   list neither of them chose. */
export function ReassignInterviewersModal({
  open,
  onOpenChange,
  interviewerUserIds,
  submitting,
  onConfirm,
}: ReassignInterviewersModalProps) {
  const { t } = useTranslation();
  const usersQuery = useUsers();
  const [selected, setSelected] = useState<string[]>(interviewerUserIds);

  // Re-seed from the interview when the dialog opens, so a cancelled edit does not linger. Adjusted
  // during render rather than in an effect, which also narrows the trigger: the effect had to list
  // interviewerUserIds as a dependency, so a background refetch returning an equal-but-new array
  // re-seeded mid-edit and discarded the recruiter's in-progress selection. Only the open
  // transition resets it now.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) setSelected(interviewerUserIds);
  }

  const toggle = (id: string) =>
    setSelected((current) =>
      current.includes(id) ? current.filter((existing) => existing !== id) : [...current, id],
    );

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('interviews.reassign.title')}
      description={t('interviews.reassign.description')}
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button
            onClick={() => onConfirm({ interviewerUserIds: selected })}
            /* An interview with nobody on it is not a valid state, and the backend rejects it —
               disabling here explains why instead of letting the request bounce. */
            disabled={submitting || selected.length === 0}
          >
            {t('interviews.reassign.confirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-2 rounded-lg border border-border p-3">
        {usersQuery.isLoading ? (
          <p className="text-sm text-text-muted">{t('interviews.form.loadingUsers')}</p>
        ) : (usersQuery.data?.length ?? 0) === 0 ? (
          <p className="text-sm text-text-muted">{t('interviews.form.noUsers')}</p>
        ) : (
          usersQuery.data?.map((user) => (
            <Checkbox
              key={user.id}
              label={fullName(user)}
              checked={selected.includes(user.id)}
              onChange={() => toggle(user.id)}
            />
          ))
        )}
      </div>

      {selected.length === 0 && (
        <p className="pt-2 text-sm text-danger">{t('interviews.reassign.atLeastOne')}</p>
      )}
    </Modal>
  );
}
