import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Checkbox, Modal } from '@/components/ui';
import { fullName, useUsers } from '@/features/users/useUsers';
import type { ReassignInterviewersRequest } from '@/types/interview';

interface ReassignInterviewersModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  interviewerUserIds: string[];
  submitting: boolean;
  onConfirm: (body: ReassignInterviewersRequest) => void;
}

/* Sends the whole panel, matching the endpoint, so concurrent edits can't interleave. */
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

  // Re-seed only when the dialog opens (during render), so a background refetch doesn't discard an
  // in-progress selection.
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
            /* The backend rejects an empty panel. */
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
