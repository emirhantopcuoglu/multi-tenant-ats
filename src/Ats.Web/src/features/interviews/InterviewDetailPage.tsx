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
import { InterviewStatusBadge } from './components/InterviewStatusBadge';
import type {
  CancelInterviewRequest,
  MarkNoShowRequest,
  ReassignInterviewersRequest,
  RescheduleRequest,
} from '@/types/interview';
import { canManageInterviews } from './interviewPermissions';
import { useInterview, useInterviewActions, useInterviewFeedback } from './useInterviews';
import { RescheduleModal } from './components/RescheduleModal';
import { CancelInterviewModal } from './components/CancelInterviewModal';
import { NoShowModal } from './components/NoShowModal';
import { ReassignInterviewersModal } from './components/ReassignInterviewersModal';
import { FeedbackList } from './components/FeedbackList';
import { FeedbackForm } from './components/FeedbackForm';

/* Wrapper so the inner view always gets an id and its hooks stay unconditional. */
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
  const feedbackQuery = useInterviewFeedback(id);
  const { reschedule, cancel, complete, noShow, reassign } = useInterviewActions(id);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [noShowOpen, setNoShowOpen] = useState(false);
  const [reassignOpen, setReassignOpen] = useState(false);

  // Uses the same cache entry as the application detail page; waits for the interview to load.
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

  const busy =
    reschedule.isPending ||
    cancel.isPending ||
    complete.isPending ||
    noShow.isPending ||
    reassign.isPending;
  const candidateName = applicationQuery.data?.candidateName;

  // Actions come from the server's can* flags, not from the status and the browser clock.
  const hasAnyAction =
    interview.canReschedule ||
    interview.canComplete ||
    interview.canMarkNoShow ||
    interview.canReassignInterviewers ||
    interview.canCancel;

  // Needs both: the interview accepts feedback and the user is on the panel. The form still handles
  // the backend's 403/409.
  const isAssignedInterviewer = user ? interview.interviewerUserIds.includes(user.id) : false;
  const canSubmitFeedback = isAssignedInterviewer && interview.canReceiveFeedback;

  // Panel members only, not canManage: the join endpoint returns 404 for anyone else. The time window
  // isn't checked here; the room page shows "too early" and "ended" itself.
  const canJoinRoom =
    isAssignedInterviewer && interview.roomToken !== null && interview.status === 'Scheduled';

  const interviewerNames = interview.interviewerUserIds
    .map((interviewerId) => {
      const user = lookup.get(interviewerId);
      return user ? fullName(user) : null;
    })
    .filter((name): name is string => name !== null);

  // The caller passes an already translated title (the type-safe t() needs literal keys).
  type VoidMutation = {
    mutate: (
      variables: void,
      options: { onSuccess: () => void; onError: (error: unknown) => void },
    ) => void;
  };
  const runAction = (mutation: VoidMutation, successTitle: string) =>
    mutation.mutate(undefined, {
      onSuccess: () => toast({ title: successTitle, tone: 'success' }),
      onError: (error) => toast({ title: actionErrorMessage(error), tone: 'danger' }),
    });

  const conflictMessage = (error: unknown): string => {
    const { code } = toApiError(error);
    if (code === 'interview.interviewer_conflict') return t('interviews.conflict.interviewer');
    if (code === 'interview.candidate_conflict') return t('interviews.conflict.candidate');
    return actionErrorMessage(error);
  };

  // A refused transition means the page is stale: the start time passed or someone else acted first.
  const actionErrorMessage = (error: unknown): string =>
    toApiError(error).code === 'interview.transition_not_allowed'
      ? t('interviews.toast.stale')
      : t('interviews.toast.error');

  const handleReschedule = (body: RescheduleRequest) =>
    reschedule.mutate(body, {
      onSuccess: () => {
        setRescheduleOpen(false);
        toast({ title: t('interviews.toast.rescheduled'), tone: 'success' });
      },
      onError: (error) => toast({ title: conflictMessage(error), tone: 'danger' }),
    });

  const handleCancel = (body: CancelInterviewRequest) =>
    cancel.mutate(body, {
      onSuccess: () => {
        setCancelOpen(false);
        toast({ title: t('interviews.toast.cancelled'), tone: 'success' });
      },
      onError: (error) => toast({ title: actionErrorMessage(error), tone: 'danger' }),
    });

  const handleNoShow = (body: MarkNoShowRequest) =>
    noShow.mutate(body, {
      onSuccess: () => {
        setNoShowOpen(false);
        toast({ title: t('interviews.toast.noShow'), tone: 'success' });
      },
      onError: (error) => toast({ title: actionErrorMessage(error), tone: 'danger' }),
    });

  const handleReassign = (body: ReassignInterviewersRequest) =>
    reassign.mutate(body, {
      onSuccess: () => {
        setReassignOpen(false);
        toast({ title: t('interviews.toast.reassigned'), tone: 'success' });
      },
      // Same wording as scheduling: a new interviewer may already be booked at this time.
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
            <InterviewStatusBadge interview={interview} />
          </div>
        </div>

        {(canJoinRoom || (canManage && hasAnyAction)) && (
          <div className="flex flex-wrap justify-end gap-2">
            {/* The only primary button: joining is what the interviewer came for. */}
            {canJoinRoom && (
              <Button onClick={() => navigate(`/interview-room/${interview.roomToken}`)}>
                {t('interviews.action.joinRoom')}
              </Button>
            )}
            {canManage && interview.canReschedule && (
              <Button variant="secondary" onClick={() => setRescheduleOpen(true)} disabled={busy}>
                {t('interviews.action.reschedule')}
              </Button>
            )}
            {canManage && interview.canComplete && (
              <Button variant="secondary" onClick={() => runAction(complete, t('interviews.toast.completed'))} disabled={busy}>
                {t('interviews.action.complete')}
              </Button>
            )}
            {canManage && interview.canMarkNoShow && (
              <Button variant="secondary" onClick={() => setNoShowOpen(true)} disabled={busy}>
                {t('interviews.action.noShow')}
              </Button>
            )}
            {canManage && interview.canReassignInterviewers && (
              <Button variant="secondary" onClick={() => setReassignOpen(true)} disabled={busy}>
                {t('interviews.action.reassign')}
              </Button>
            )}
            {canManage && interview.canCancel && (
              <Button variant="danger" onClick={() => setCancelOpen(true)} disabled={busy}>
                {t('interviews.action.cancel')}
              </Button>
            )}
          </div>
        )}
      </Card>

      {/* Otherwise an elapsed, unresolved interview looks the same as an upcoming one. */}
      {canManage && interview.isAwaitingOutcome && (
        <Card className="border-warning/40 bg-warning/5">
          <p className="text-sm text-text">{t('interviews.awaitingOutcome.title')}</p>
          <p className="pt-1 text-sm text-text-muted">{t('interviews.awaitingOutcome.body')}</p>
        </Card>
      )}

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

        {interview.noShowParty && (
          <InfoRow label={t('interviews.noShow.party')}>
            {t(`noShowParty.${interview.noShowParty}`)}
          </InfoRow>
        )}

        {interview.cancellationReason && (
          <InfoRow label={t('interviews.cancel.reason')}>
            {t(`cancellationReason.${interview.cancellationReason}`)}
          </InfoRow>
        )}

        {interview.cancellationNote && (
          <InfoRow label={t('interviews.cancel.note')}>
            <span className="whitespace-pre-wrap">{interview.cancellationNote}</span>
          </InfoRow>
        )}
      </Card>

      <Card className="space-y-3">
        <h3 className="text-sm font-semibold text-text">{t('interviews.feedback.panelTitle')}</h3>
        <FeedbackList summary={feedbackQuery.data} isLoading={feedbackQuery.isLoading} />
      </Card>

      {/* Hidden once submitted: feedback can't be edited. */}
      {canSubmitFeedback && feedbackQuery.data && !feedbackQuery.data.hasCallerSubmitted && (
        <Card className="space-y-3">
          <h3 className="text-sm font-semibold text-text">{t('interviews.feedback.title')}</h3>
          <FeedbackForm interviewId={id} />
        </Card>
      )}

      {!canSubmitFeedback && isAssignedInterviewer && (
        <Card>
          <p className="text-sm text-text-muted">
            {interview.status === 'Cancelled' || interview.status === 'NoShow'
              ? t('interviews.feedback.lockedNoOutcome')
              : t('interviews.feedback.lockedNotYet')}
          </p>
        </Card>
      )}

      <RescheduleModal
        open={rescheduleOpen}
        onOpenChange={setRescheduleOpen}
        scheduledAtUtc={interview.scheduledAtUtc}
        durationMinutes={interview.durationMinutes}
        submitting={reschedule.isPending}
        onConfirm={handleReschedule}
      />

      <CancelInterviewModal
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        submitting={cancel.isPending}
        onConfirm={handleCancel}
      />

      <NoShowModal
        open={noShowOpen}
        onOpenChange={setNoShowOpen}
        submitting={noShow.isPending}
        onConfirm={handleNoShow}
      />

      <ReassignInterviewersModal
        open={reassignOpen}
        onOpenChange={setReassignOpen}
        interviewerUserIds={interview.interviewerUserIds}
        submitting={reassign.isPending}
        onConfirm={handleReassign}
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
