import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Checkbox, Field, Input, Modal, Select, Textarea, useToast } from '@/components/ui';
import { fullName, useUsers } from '@/features/users/useUsers';
import { INTERVIEW_TYPES, type InterviewType } from '@/types/enums';
import { useActiveApplicationOptions, useScheduleInterview } from '../useInterviews';

interface ScheduleInterviewModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** When scheduling from an application's detail page the candidate is fixed: the picker is hidden
      and this id is used directly. Omit it on the standalone list to show the candidate picker. */
  applicationId?: string;
  /** Shown read-only when applicationId is fixed, so the user sees who they are scheduling for. */
  candidateName?: string;
  /** Called with the new interview's id after a successful schedule (the caller may navigate to it). */
  onScheduled?: (id: string) => void;
}

const DEFAULT_DURATION_MINUTES = 60;
const MIN_DURATION_MINUTES = 1;

interface FormErrors {
  applicationId?: string;
  scheduledAt?: string;
  duration?: string;
}

/* Schedule form. It collects a local date+time and converts to a UTC ISO string before sending, since
   the API stores scheduledAtUtc in UTC. The interviewer panel is optional here to match the backend
   (an interview can be scheduled before the panel is finalised); feedback later gates on it. */
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

  // Only fetch the candidate list when the picker is actually shown (no fixed application).
  const needsPicker = !applicationId;
  const applicationsQuery = useActiveApplicationOptions(open && needsPicker);

  const [pickedApplicationId, setPickedApplicationId] = useState('');
  const [type, setType] = useState<InterviewType>(INTERVIEW_TYPES[0]);
  const [scheduledAt, setScheduledAt] = useState('');
  const [durationMinutes, setDurationMinutes] = useState(String(DEFAULT_DURATION_MINUTES));
  const [location, setLocation] = useState('');
  const [interviewerUserIds, setInterviewerUserIds] = useState<string[]>([]);
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});

  // Reset every field when the modal (re)opens, so a previous draft never lingers.
  useEffect(() => {
    if (open) {
      setPickedApplicationId('');
      setType(INTERVIEW_TYPES[0]);
      setScheduledAt('');
      setDurationMinutes(String(DEFAULT_DURATION_MINUTES));
      setLocation('');
      setInterviewerUserIds([]);
      setNotes('');
      setErrors({});
    }
  }, [open]);

  const toggleInterviewer = (id: string) =>
    setInterviewerUserIds((current) =>
      current.includes(id) ? current.filter((existing) => existing !== id) : [...current, id],
    );

  const handleSubmit = () => {
    const effectiveApplicationId = applicationId ?? pickedApplicationId;
    const duration = Number(durationMinutes);

    const nextErrors: FormErrors = {};
    if (!effectiveApplicationId) nextErrors.applicationId = t('interviews.form.applicationRequired');
    if (!scheduledAt) nextErrors.scheduledAt = t('interviews.form.whenRequired');
    if (!Number.isFinite(duration) || duration < MIN_DURATION_MINUTES)
      nextErrors.duration = t('interviews.form.durationInvalid');

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    schedule.mutate(
      {
        applicationId: effectiveApplicationId,
        type,
        // datetime-local yields a local wall-clock time; convert to the UTC instant the API expects.
        scheduledAtUtc: new Date(scheduledAt).toISOString(),
        durationMinutes: duration,
        location: location.trim() || undefined,
        interviewerUserIds,
        notes: notes.trim() || undefined,
      },
      {
        onSuccess: (id) => {
          onOpenChange(false);
          toast({ title: t('interviews.toast.scheduled'), tone: 'success' });
          onScheduled?.(id);
        },
        onError: () => toast({ title: t('interviews.toast.error'), tone: 'danger' }),
      },
    );
  };

  const applications = applicationsQuery.data?.items ?? [];

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
        {needsPicker ? (
          <Field label={t('interviews.form.candidate')} error={errors.applicationId}>
            {({ id, describedById, invalid }) => (
              <Select
                id={id}
                aria-describedby={describedById}
                invalid={invalid}
                value={pickedApplicationId}
                onChange={(event) => setPickedApplicationId(event.target.value)}
                disabled={applicationsQuery.isLoading}
              >
                <option value="">{t('interviews.form.candidatePlaceholder')}</option>
                {applications.map((application) => (
                  <option key={application.id} value={application.id}>
                    {application.candidateName}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        ) : (
          <div className="space-y-1.5">
            <span className="block text-sm font-medium text-text">{t('interviews.form.candidate')}</span>
            <p className="text-sm text-text-muted">{candidateName ?? '—'}</p>
          </div>
        )}

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
          <Field label={t('interviews.form.when')} error={errors.scheduledAt}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="datetime-local"
                aria-describedby={describedById}
                invalid={invalid}
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
              />
            )}
          </Field>

          <Field label={t('interviews.form.duration')} error={errors.duration}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="number"
                min={MIN_DURATION_MINUTES}
                aria-describedby={describedById}
                invalid={invalid}
                value={durationMinutes}
                onChange={(event) => setDurationMinutes(event.target.value)}
              />
            )}
          </Field>
        </div>

        <Field label={t('interviews.form.location')}>
          {({ id }) => (
            <Input
              id={id}
              value={location}
              onChange={(event) => setLocation(event.target.value)}
              placeholder={t('interviews.form.locationPlaceholder')}
            />
          )}
        </Field>

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
