import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Checkbox, Field, Input, Modal, Select, Textarea, useToast } from '@/components/ui';
import { fullName, useUsers } from '@/features/users/useUsers';
import { toApiError } from '@/lib/problemDetails';
import {
  DEFAULT_INTERVIEW_DURATION,
  INTERVIEW_DURATION_OPTIONS,
  INTERVIEW_TYPES,
  type InterviewType,
} from '@/types/enums';
import { useScheduleInterview } from '../useInterviews';
import { MINIMUM_LEAD_MINUTES, isSlotTooSoon, toScheduledAt } from '../scheduleValidation';

interface ScheduleInterviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The application this interview is for — always fixed now that scheduling is candidate-contextual. */
  applicationId: string;
  /** Shown read-only so the user sees who they are scheduling for. */
  candidateName?: string;
  /** Called with the new interview's id after a successful schedule (the caller may navigate/refetch). */
  onScheduled?: (id: string) => void;
}

interface FormErrors {
  date?: string;
  time?: string;
}

/* Today's date as the YYYY-MM-DD a native date input expects, in the viewer's local zone (so it
   doesn't slip a day near midnight the way a naive UTC slice would). Used as the min so a past day
   can't be picked; the future-instant check below still guards a past time on today. */
function localDateInputValue(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

/* Schedule form. It collects a local date + time and converts to a UTC ISO string before sending,
   since the API stores scheduledAtUtc in UTC. Interviewers are optional here (an interview can be
   scheduled before the panel is finalised); feedback later gates on the panel. */
export function ScheduleInterviewModal({
  open,
  onOpenChange,
  applicationId,
  candidateName,
  onScheduled,
}: ScheduleInterviewModalProps) {
  const { t } = useTranslation();
  const { toast } = useToast();
  const schedule = useScheduleInterview();
  const usersQuery = useUsers();

  const [type, setType] = useState<InterviewType>(INTERVIEW_TYPES[0]);
  const [date, setDate] = useState('');
  const [time, setTime] = useState('');
  const [durationMinutes, setDurationMinutes] = useState<number>(DEFAULT_INTERVIEW_DURATION);
  const [interviewerUserIds, setInterviewerUserIds] = useState<string[]>([]);
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});

  // Reset every field when the modal opens, so a previous draft never lingers. Adjusted during
  // render rather than in an effect: React discards the in-progress output and re-renders before
  // committing, so the cleared form is what reaches the DOM. An effect would paint the stale draft
  // first and clear it afterwards, which is the cascading render the linter flags.
  const [prevOpen, setPrevOpen] = useState(open);
  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setType(INTERVIEW_TYPES[0]);
      setDate('');
      setTime('');
      setDurationMinutes(DEFAULT_INTERVIEW_DURATION);
      setInterviewerUserIds([]);
      setNotes('');
      setErrors({});
    }
  }

  const toggleInterviewer = (id: string) =>
    setInterviewerUserIds((current) =>
      current.includes(id) ? current.filter((existing) => existing !== id) : [...current, id],
    );

  const handleSubmit = () => {
    const nextErrors: FormErrors = {};
    if (!date) nextErrors.date = t('interviews.form.dateRequired');
    if (!time) nextErrors.time = t('interviews.form.timeRequired');

    // datetime-local yields a local wall-clock time; combining date + time the same way and then
    // checking the instant keeps a past slot (or an invalid combination) out.
    const scheduledAt = toScheduledAt(date, time);
    if (scheduledAt && isSlotTooSoon(scheduledAt))
      nextErrors.time = t('interviews.form.whenTooSoon', { count: MINIMUM_LEAD_MINUTES });

    if (Object.keys(nextErrors).length > 0 || !scheduledAt) {
      setErrors(nextErrors);
      return;
    }

    schedule.mutate(
      {
        applicationId,
        type,
        scheduledAtUtc: scheduledAt.toISOString(),
        durationMinutes,
        interviewerUserIds,
        notes: notes.trim() || undefined,
      },
      {
        onSuccess: (id) => {
          onOpenChange(false);
          toast({ title: t('interviews.toast.scheduled'), tone: 'success' });
          onScheduled?.(id);
        },
        onError: (error) => toast({ title: conflictMessage(error), tone: 'danger' }),
      },
    );
  };

  // Turn the backend's 409 conflict codes into a specific message; anything else is the generic error.
  const conflictMessage = (error: unknown): string => {
    const { code } = toApiError(error);
    if (code === 'interview.interviewer_conflict') return t('interviews.conflict.interviewer');
    if (code === 'interview.candidate_conflict') return t('interviews.conflict.candidate');
    return t('interviews.toast.error');
  };

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={t('interviews.scheduleTitle')}
      description={t('interviews.scheduleSub')}
      className="max-w-lg"
      footer={
        <>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={schedule.isPending}>
            {t('common.cancel')}
          </Button>
          <Button onClick={handleSubmit} disabled={schedule.isPending}>
            {t('interviews.schedule')}
          </Button>
        </>
      }
    >
      <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
        <div className="space-y-1.5">
          <span className="block text-sm font-medium text-text">{t('interviews.form.candidate')}</span>
          <p className="text-sm text-text-muted">{candidateName ?? '—'}</p>
        </div>

        <Field label={t('interviews.form.type')}>
          {({ id }) => (
            <Select id={id} value={type} onChange={(event) => setType(event.target.value as InterviewType)}>
              {INTERVIEW_TYPES.map((value) => (
                <option key={value} value={value}>
                  {t(`interviewType.${value}`)}
                </option>
              ))}
            </Select>
          )}
        </Field>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Field label={t('interviews.form.date')} error={errors.date}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="date"
                min={localDateInputValue(new Date())}
                aria-describedby={describedById}
                invalid={invalid}
                value={date}
                onChange={(event) => setDate(event.target.value)}
              />
            )}
          </Field>

          <Field label={t('interviews.form.time')} error={errors.time}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="time"
                aria-describedby={describedById}
                invalid={invalid}
                value={time}
                onChange={(event) => setTime(event.target.value)}
              />
            )}
          </Field>
        </div>

        <div className="space-y-1.5">
          <span className="block text-sm font-medium text-text">{t('interviews.form.duration')}</span>
          <div className="flex flex-wrap gap-2">
            {INTERVIEW_DURATION_OPTIONS.map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={durationMinutes === value}
                onClick={() => setDurationMinutes(value)}
                className={
                  'rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ' +
                  (durationMinutes === value
                    ? 'border-transparent bg-accent text-accent-fg'
                    : 'border-border bg-card text-text hover:bg-divider')
                }
              >
                {t('interviews.minutesShort', { count: value })}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-1.5">
          <span className="block text-sm font-medium text-text">{t('interviews.form.interviewers')}</span>
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
                  checked={interviewerUserIds.includes(user.id)}
                  onChange={() => toggleInterviewer(user.id)}
                />
              ))
            )}
          </div>
        </div>

        <Field label={t('interviews.form.notes')}>
          {({ id }) => (
            <Textarea
              id={id}
              rows={3}
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              placeholder={t('interviews.form.notesPlaceholder')}
            />
          )}
        </Field>
      </div>
    </Modal>
  );
}
