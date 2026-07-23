import { useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { Badge, Button, Card, Skeleton, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { fullName, useUserLookup } from '@/features/users/useUsers';
import { getApplication } from '@/features/applications/applicationsApi';
import { applicationDetailKey } from '@/features/applications/useApplicationDetail';
import { interviewStatusTone } from '@/lib/statusColors';
import type { RescheduleRequest } from '@/types/interview';
import { canManageInterviews } from './interviewPermissions';
import { useInterview, useInterviewActions } from './useInterviews';
import { RescheduleModal } from './components/RescheduleModal';
import { FeedbackForm } from './components/FeedbackForm';

/* Thin wrapper so the inner view can take a guaranteed-present id and keep its hooks unconditional. */
export function InterviewDetailPage() {
  const { id } = useParams();
  if (!id) return <Navigate to="/interviews" replace />;
  return <InterviewDetailView id={id} />;
}

function InterviewDetailView({ id }: { id: string }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { toast } = useToast();
  const { role, user } = useAuth();
  const canManage = canManageInterviews(role);
  const lookup = useUserLookup();

  const { data: interview, isLoading, isError } = useInterview(id);
  const { reschedule, cancel, complete, noShow } = useInterviewActions(id);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);

  // The interview carries only an applicationId; resolve the candidate from the same cache entry the
  // application detail page uses, gated until the interview (and so the id) has loaded.
  const applicationId = interview?.applicationId;
  const applicationQuery = useQuery({
    queryKey: applicationDetailKey(applicationId ?? ''),
    queryFn: () => getApplication(applicationId as string),
    enabled: Boolean(applicationId),
  });

  if (isLoading) {
    return (
      <Card className="mx-auto max-w-3xl">
        <Skeleton className="h-48 w-full" />
      </Card>
    );
  }

  if (isError || !interview) {
    return (
      <Card className="mx-auto max-w-3xl space-y-3 text-center">
        <p className="text-sm text-text-muted">{t('interviews.detail.notFound')}</p>
        <Button variant="secondary" onClick={() => navigate('/interviews')}>
          {t('interviews.detail.back')}
        </Button>
      </Card>
    );
  }

  const dateTimeFormatter = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'full',
    timeStyle: 'short',
  });

  const busy = reschedule.isPending || cancel.isPending || complete.isPending || noShow.isPending;
  const isScheduled = interview.status === 'Scheduled';
  const candidateName = applicationQuery.data?.candidateName;

  // Feedback gating mirrors the backend: only an assigned interviewer may submit, and never for a
  // cancelled interview. The form still maps the backend's 403/409 as the final authority.
  const isAssignedInterviewer = user ? interview.interviewerUserIds.includes(user.id) : false;
  const canSubmitFeedback = isAssignedInterviewer && interview.status !== 'Cancelled';

  const interviewerNames = interview.interviewerUserIds
    .map((interviewerId) => {
      const user = lookup.get(interviewerId);
      return user ? fullName(user) : null;
    })
    .filter((name): name is string => name !== null);

  // successTitle is already resolved by the caller (the type-safe t() needs a literal key, not a
  // dynamic string), so this helper only wires the shared success/error toasts to a void mutation.
  type VoidMutation = {
    mutate: (variables: void, options: { onSuccess: () => void; onError: () => void }) => void;
  };
  const runAction = (mutation: VoidMutation, successTitle: string) =>
    mutation.mutate(undefined, {
      onSuccess: () => toast({ title: successTitle, tone: 'success' }),
      onError: () => toast({ title: t('interviews.toast.error'), tone: 'danger' }),
    });

  // Turn the backend's 409 conflict codes into a specific message; anything else is the generic error.
  const conflictMessage = (error: unknown): string => {
    const { code } = toApiError(error);
    if (code === 'interview.interviewer_conflict') return t('interviews.conflict.interviewer');
    if (code === 'interview.candidate_conflict') return t('interviews.conflict.candidate');
    return t('interviews.toast.error');
  };

  const handleReschedule = (body: RescheduleRequest) =>
    reschedule.mutate(body, {
      onSuccess: () => {
        setRescheduleOpen(false);
        toast({ title: t('interviews.toast.rescheduled'), tone: 'success' });
      },
      onError: (error) => toast({ title: conflictMessage(error), tone: 'danger' }),
    });

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <Link to="/interviews" className="text-sm text-text-muted transition-colors hover:text-text">
        ← {t('interviews.detail.back')}
      </Link>

      <Card className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-1">
          <h2 className="text-lg font-semibold text-text">{candidateName ?? '—'}</h2>
          {applicationId && (
            <Link to={`/applications/${applicationId}`} className="text-sm text-accent hover:underline">
              {t('interviews.detail.viewApplication')}
            </Link>
          )}
          <div className="flex items-center gap-2 pt-1">
            <Badge tone="neutral">{t(`interviewType.${interview.type}`)}</Badge>
            <Badge tone={interviewStatusTone[interview.status]} dot>
              {t(`status.${interview.status}`)}
            </Badge>
          </div>
        </div>

        {canManage && isScheduled && (
          <div className="flex flex-wrap justify-end gap-2">
            <Button variant="secondary" onClick={() => setRescheduleOpen(true)} disabled={busy}>
              {t('interviews.action.reschedule')}
            </Button>
            <Button variant="secondary" onClick={() => runAction(complete, t('interviews.toast.completed'))} disabled={busy}>
              {t('interviews.action.complete')}
            </Button>
            <Button variant="secondary" onClick={() => runAction(noShow, t('interviews.toast.noShow'))} disabled={busy}>
              {t('interviews.action.noShow')}
            </Button>
            <Button variant="danger" onClick={() => runAction(cancel, t('interviews.toast.cancelled'))} disabled={busy}>
              {t('interviews.action.cancel')}
            </Button>
          </div>
        )}
      </Card>

      <Card className="space-y-4">
        <InfoRow label={t('interviews.form.when')}>
          {dateTimeFormatter.format(new Date(interview.scheduledAtUtc))}
        </InfoRow>
        <InfoRow label={t('interviews.form.duration')}>
          {t('interviews.minutesShort', { count: interview.durationMinutes })}
        </InfoRow>
        <InfoRow label={t('interviews.form.interviewers')}>
          {interviewerNames.length > 0 ? interviewerNames.join(', ') : <span className="text-text-muted">—</span>}
        </InfoRow>
        <InfoRow label={t('interviews.form.notes')}>
          {interview.notes ? (
            <span className="whitespace-pre-wrap">{interview.notes}</span>
          ) : (
            <span className="text-text-muted">—</span>
          )}
        </InfoRow>
      </Card>

      <Card className="space-y-3">
        <h3 className="text-sm font-semibold text-text">{t('interviews.feedback.title')}</h3>
        {canSubmitFeedback ? (
          <FeedbackForm interviewId={id} />
        ) : (
          <p className="text-sm text-text-muted">
            {interview.status === 'Cancelled'
              ? t('interviews.feedback.lockedCancelled')
              : t('interviews.feedback.locked')}
          </p>
        )}
      </Card>

      <RescheduleModal
        open={rescheduleOpen}
        onOpenChange={setRescheduleOpen}
        scheduledAtUtc={interview.scheduledAtUtc}
        durationMinutes={interview.durationMinutes}
        submitting={reschedule.isPending}
        onConfirm={handleReschedule}
      />
    </div>
  );
}

function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1 sm:flex-row sm:gap-4">
      <span className="w-32 shrink-0 text-sm font-medium text-text-muted">{label}</span>
      <span className="text-sm text-text">{children}</span>
    </div>
  );
}
