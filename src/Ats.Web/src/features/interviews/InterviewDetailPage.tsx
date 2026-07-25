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
import { useInterview, useInterviewActions } from './useInterviews';
import { RescheduleModal } from './components/RescheduleModal';
import { CancelInterviewModal } from './components/CancelInterviewModal';
import { NoShowModal } from './components/NoShowModal';
import { ReassignInterviewersModal } from './components/ReassignInterviewersModal';
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
  const { reschedule, cancel, complete, noShow, reassign } = useInterviewActions(id);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [noShowOpen, setNoShowOpen] = useState(false);
  const [reassignOpen, setReassignOpen] = useState(false);

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

  const busy =
    reschedule.isPending ||
    cancel.isPending ||
    complete.isPending ||
    noShow.isPending ||
    reassign.isPending;
  const candidateName = applicationQuery.data?.candidateName;

  // Which actions exist is the domain's answer, not ours — the page renders interview.can* rather
  // than deriving anything from the status and the browser's clock. Before/after the start time the
  // legal pair differs: an interview that has not begun can be moved or called off, one that has can
  // only be recorded as completed or missed.
  const hasAnyAction =
    interview.canReschedule ||
    interview.canComplete ||
    interview.canMarkNoShow ||
    interview.canReassignInterviewers ||
    interview.canCancel;

  // Feedback needs both halves: the backend says the interview is evaluable, and the caller must be
  // one of its assigned interviewers. The form still maps the backend's 403/409 as final authority.
  const isAssignedInterviewer = user ? interview.interviewerUserIds.includes(user.id) : false;
  const canSubmitFeedback = isAssignedInterviewer && interview.canReceiveFeedback;

  const interviewerNames = interview.interviewerUserIds
    .map((interviewerId) => {
      const user = lookup.get(interviewerId);
      return user ? fullName(user) : null;
    })
    .filter((name): name is string => name !== null);

  // successTitle is already resolved by the caller (the type-safe t() needs a literal key, not a
  // dynamic string), so this helper only wires the shared success/error toasts to a void mutation.
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

  // Turn the backend's 409 conflict codes into a specific message; anything else is the generic error.
  const conflictMessage = (error: unknown): string => {
    const { code } = toApiError(error);
    if (code === 'interview.interviewer_conflict') return t('interviews.conflict.interviewer');
    if (code === 'interview.candidate_conflict') return t('interviews.conflict.candidate');
    return actionErrorMessage(error);
  };

  // The buttons are gated on the server's own flags, so a rejected transition means this page is
  // showing a stale snapshot — most likely the clock crossed the start time, or someone else acted
  // on the interview first. Saying so is more useful than "something went wrong".
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
      // Reuses the scheduling conflict wording: a replacement interviewer can already be booked
      // over this slot, and "an interviewer already has another interview at this time" is the
      // useful thing to say.
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

        {canManage && hasAnyAction && (
          <div className="flex flex-wrap justify-end gap-2">
            {interview.canReschedule && (
              <Button variant="secondary" onClick={() => setRescheduleOpen(true)} disabled={busy}>
                {t('interviews.action.reschedule')}
              </Button>
            )}
            {interview.canComplete && (
              <Button variant="secondary" onClick={() => runAction(complete, t('interviews.toast.completed'))} disabled={busy}>
                {t('interviews.action.complete')}
              </Button>
            )}
            {interview.canMarkNoShow && (
              <Button variant="secondary" onClick={() => setNoShowOpen(true)} disabled={busy}>
                {t('interviews.action.noShow')}
              </Button>
            )}
            {interview.canReassignInterviewers && (
              <Button variant="secondary" onClick={() => setReassignOpen(true)} disabled={busy}>
                {t('interviews.action.reassign')}
              </Button>
            )}
            {interview.canCancel && (
              <Button variant="danger" onClick={() => setCancelOpen(true)} disabled={busy}>
                {t('interviews.action.cancel')}
              </Button>
            )}
          </div>
        )}
      </Card>

      {/* The prompt the screen was missing: an elapsed interview nobody resolved looks identical to
          an upcoming one, so the recruiter has no cue that it is waiting on them. */}
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

        {/* The outcome details, shown rather than only stored: a reason nobody can read back would
            be the same write-only field this rework exists to remove. */}
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
        <h3 className="text-sm font-semibold text-text">{t('interviews.feedback.title')}</h3>
        {canSubmitFeedback ? (
          <FeedbackForm interviewId={id} />
        ) : (
          <p className="text-sm text-text-muted">
            {interview.status === 'Cancelled' || interview.status === 'NoShow'
              ? t('interviews.feedback.lockedNoOutcome')
              : !isAssignedInterviewer
                ? t('interviews.feedback.locked')
                : t('interviews.feedback.lockedNotYet')}
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
