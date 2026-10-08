import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, EmptyState, Skeleton, useToast } from '@/components/ui';
import { applicationStatusTone } from '@/lib/statusColors';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useCandidateApplication } from '../useCandidateApplication';
import { useWithdrawApplication } from '../useWithdrawApplication';
import { buildTrackingSteps } from '../trackingSteps';
import { TrackingTimeline } from '../components/TrackingTimeline';
import { CandidateInterviewsCard } from '../components/CandidateInterviewsCard';
import { WithdrawApplicationDialog } from '../components/WithdrawApplicationDialog';
import type { CandidateApplicationDetail } from '../candidateApplicationsApi';

/* The transparent tracking view at /candidate/applications/{id}: everything that happened to the
   application (dated, colour-coded) followed by where it sits in the company's full pipeline and
   what is still ahead. The backend guarantees the data is candidate-safe; this page only renders. */
export function CandidateApplicationDetailPage() {
  const { t } = useTranslation();
  const { id = '' } = useParams();
  const query = useCandidateApplication(id);

  return (
    <PublicLayout>
      <div className="space-y-6">
        <Link
          to="/candidate/applications"
          className="text-sm text-text-muted transition-colors hover:text-accent"
        >
          {t('candidatePortal.tracking.back')}
        </Link>

        {query.isLoading ? (
          <div className="space-y-4" aria-busy="true">
            <Skeleton className="h-28 w-full" />
            <Skeleton className="h-72 w-full" />
          </div>
        ) : query.isError || !query.data ? (
          <EmptyState
            title={t('candidatePortal.tracking.notFound')}
            action={
              <Link
                to="/candidate/applications"
                className="text-sm font-medium text-accent hover:underline"
              >
                {t('candidatePortal.tracking.back')}
              </Link>
            }
          />
        ) : (
          <TrackingView detail={query.data} />
        )}
      </div>
    </PublicLayout>
  );
}

function TrackingView({ detail }: { detail: CandidateApplicationDetail }) {
  const { t, i18n } = useTranslation();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const withdraw = useWithdrawApplication(detail.id);
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'long',
    timeStyle: 'short',
  });
  const steps = buildTrackingSteps(detail);

  const handleWithdraw = () => {
    withdraw.mutate(undefined, {
      onSuccess: () => {
        setDialogOpen(false);
        toast({ title: t('candidatePortal.withdraw.success'), tone: 'success' });
      },
      onError: (error) => {
        setDialogOpen(false);
        // 409 is the application having closed since this page loaded — a stale tab, not a failure the
        // candidate can act on, so it gets the neutral tone and a message that explains what they are
        // about to see rather than offering a retry.
        const alreadyClosed = isAxiosError(error) && error.response?.status === 409;
        toast({
          title: alreadyClosed
            ? t('candidatePortal.withdraw.alreadyClosed')
            : t('candidatePortal.withdraw.error'),
          tone: alreadyClosed ? 'default' : 'danger',
        });
      },
    });
  };

  return (
    <>
      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">
            <Link
              to={`/${detail.companySlug}/jobs/${detail.jobSlug}`}
              className="transition-colors hover:text-accent"
            >
              {detail.jobTitle}
            </Link>
          </h1>
          <Badge tone={applicationStatusTone[detail.status]} dot>
            {t(`status.${detail.status}`)}
          </Badge>
          {/* Only an application still in play can be withdrawn, and the backend enforces that too —
              this just keeps the UI from offering an action it knows will be refused. */}
          {detail.status === 'Active' && (
            <Button
              variant="ghost"
              className="ml-auto"
              onClick={() => setDialogOpen(true)}
              disabled={withdraw.isPending}
            >
              {t('candidatePortal.withdraw.action')}
            </Button>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-text-muted">
          <Link to={`/${detail.companySlug}`} className="text-accent hover:underline">
            {detail.companyName}
          </Link>
          <span aria-hidden="true">·</span>
          <span>
            {t('candidatePortal.tracking.appliedOn', {
              date: dateFormatter.format(new Date(detail.appliedAtUtc)),
            })}
          </span>
        </div>
      </header>

      <CandidateInterviewsCard interviews={detail.interviews} />

      <Card className="space-y-5">
        <h2 className="text-lg font-semibold tracking-tight">
          {t('candidatePortal.tracking.title')}
        </h2>
        <TrackingTimeline steps={steps} detail={detail} dateFormatter={dateFormatter} />
      </Card>

      <WithdrawApplicationDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onConfirm={handleWithdraw}
        submitting={withdraw.isPending}
        jobTitle={detail.jobTitle}
        companyName={detail.companyName}
      />
    </>
  );
}
