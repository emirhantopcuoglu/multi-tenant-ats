import { useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Skeleton, TabPanel, Tabs, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { canManageInterviews } from '@/features/interviews/interviewPermissions';
import { ScheduleInterviewModal } from '@/features/interviews/components/ScheduleInterviewModal';
import { canManageApplications } from './applicationPermissions';
import { getCvDownloadUrl } from './applicationsApi';
import { useJobStages } from './useApplications';
import { useApplication, useApplicationActions, useApplicationActivities } from './useApplicationDetail';
import { ApplicationHeader } from './components/ApplicationHeader';
import { CorrectStageDialog } from './components/CorrectStageDialog';
import { HireDialog } from './components/HireDialog';
import { RejectDialog } from './components/RejectDialog';
import { CvAnalysisTab } from './components/CvAnalysisTab';
import { ActivityTimeline } from './components/ActivityTimeline';
import { ApplicationInterviewsTab } from './components/ApplicationInterviewsTab';

/* Thin wrapper so the inner view can take a guaranteed-present id and keep its hooks unconditional. */
export function ApplicationDetailPage() {
  const { id } = useParams();
  if (!id) return <Navigate to="/applications" replace />;
  return <ApplicationDetailView id={id} />;
}

function ApplicationDetailView({ id }: { id: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { toast } = useToast();
  const { role } = useAuth();
  const canManage = canManageApplications(role);
  const canScheduleInterview = canManageInterviews(role);

  const { data: application, isLoading, isError } = useApplication(id);
  const activitiesQuery = useApplicationActivities(id);
  const stagesQuery = useJobStages(application?.jobId);
  const { move, correctStage, reject, hire } = useApplicationActions(id);

  const [tab, setTab] = useState('cv');
  const [rejectOpen, setRejectOpen] = useState(false);
  const [hireOpen, setHireOpen] = useState(false);
  const [correctStageOpen, setCorrectStageOpen] = useState(false);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [cvLoading, setCvLoading] = useState(false);

  if (isLoading) {
    return (
      <Card className="mx-auto max-w-3xl">
        <Skeleton className="h-48 w-full" />
      </Card>
    );
  }

  if (isError || !application) {
    return (
      <Card className="mx-auto max-w-3xl space-y-3 text-center">
        <p className="text-sm text-text-muted">{t('applicationDetail.notFound')}</p>
        <Button variant="secondary" onClick={() => navigate('/applications')}>
          {t('applicationDetail.back')}
        </Button>
      </Card>
    );
  }

  const busy = move.isPending || correctStage.isPending || reject.isPending || hire.isPending;

  const handleMove = (stageId: string) => {
    // Moving into the Interview stage doesn't move the application directly — it opens the same
    // scheduling dialog as the standalone "Schedule interview" button. The move itself happens
    // server-side once an interview is actually scheduled (AdvanceToInterviewStageConsumer).
    const targetStage = stagesQuery.data?.find((stage) => stage.id === stageId);
    if (targetStage?.type === 'Interview') {
      setScheduleOpen(true);
      return;
    }

    move.mutate(stageId, {
      onSuccess: () => toast({ title: t('applicationDetail.toast.moved'), tone: 'success' }),
      onError: () => toast({ title: t('applicationDetail.toast.error'), tone: 'danger' }),
    });
  };

  const handleCorrectStage = (targetStageId: string, reason: string) =>
    correctStage.mutate(
      { targetStageId, reason },
      {
        onSuccess: () => {
          setCorrectStageOpen(false);
          toast({ title: t('applicationDetail.toast.corrected'), tone: 'success' });
        },
        onError: () => toast({ title: t('applicationDetail.toast.error'), tone: 'danger' }),
      },
    );

  const handleReject = (reason: string) =>
    reject.mutate(reason, {
      onSuccess: () => {
        setRejectOpen(false);
        toast({ title: t('applicationDetail.toast.rejected'), tone: 'success' });
      },
      onError: () => toast({ title: t('applicationDetail.toast.error'), tone: 'danger' }),
    });

  const handleHire = () =>
    hire.mutate(undefined, {
      onSuccess: () => {
        setHireOpen(false);
        toast({ title: t('applicationDetail.toast.hired'), tone: 'success' });
      },
      onError: () => toast({ title: t('applicationDetail.toast.error'), tone: 'danger' }),
    });

  const openCv = async () => {
    try {
      setCvLoading(true);
      const { url } = await getCvDownloadUrl(id);
      window.open(url, '_blank', 'noopener');
    } catch {
      toast({ title: t('applicationDetail.toast.error'), tone: 'danger' });
    } finally {
      setCvLoading(false);
    }
  };

  // A follow-up interview can be scheduled only once the application has actually reached the
  // Interview stage: the first interview comes from moving it there (which opens this same modal),
  // and additional rounds come from the Interviews tab below. Before that stage there's nothing to
  // schedule against, so the button stays hidden.
  const stages = stagesQuery.data ?? [];
  const interviewStageOrder = stages.find((stage) => stage.type === 'Interview')?.order;
  const currentStageOrder = stages.find((stage) => stage.id === application.stageId)?.order;
  const canScheduleInterviewNow =
    canScheduleInterview &&
    application.status === 'Active' &&
    interviewStageOrder !== undefined &&
    currentStageOrder !== undefined &&
    currentStageOrder >= interviewStageOrder;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <Link to="/applications" className="text-sm text-text-muted transition-colors hover:text-text">
        ← {t('applicationDetail.back')}
      </Link>

      <ApplicationHeader
        application={application}
        stages={stagesQuery.data ?? []}
        canManage={canManage}
        onMove={handleMove}
        onCorrectStageClick={() => setCorrectStageOpen(true)}
        onHireClick={() => setHireOpen(true)}
        onRejectClick={() => setRejectOpen(true)}
        busy={busy}
      />

      <Card>
        <Tabs
          value={tab}
          onValueChange={setTab}
          items={[
            { value: 'cv', label: t('applicationDetail.tabs.cv') },
            { value: 'cover', label: t('applicationDetail.tabs.cover') },
            { value: 'analysis', label: t('applicationDetail.tabs.analysis') },
            { value: 'interviews', label: t('applicationDetail.tabs.interviews') },
            { value: 'activity', label: t('applicationDetail.tabs.activity') },
          ]}
        >
          <TabPanel value="cv">
            {application.hasCv ? (
              <Button variant="secondary" onClick={openCv} disabled={cvLoading}>
                {t('applicationDetail.cv.open')}
              </Button>
            ) : (
              <p className="text-sm text-text-muted">{t('applicationDetail.cv.none')}</p>
            )}
          </TabPanel>

          <TabPanel value="cover">
            {application.coverLetter ? (
              <p className="whitespace-pre-wrap text-sm leading-relaxed text-text">
                {application.coverLetter}
              </p>
            ) : (
              <p className="text-sm text-text-muted">{t('applicationDetail.cover.none')}</p>
            )}
          </TabPanel>

          <TabPanel value="analysis">
            <CvAnalysisTab applicationId={id} />
          </TabPanel>

          <TabPanel value="interviews">
            <ApplicationInterviewsTab
              applicationId={id}
              canSchedule={canScheduleInterviewNow}
              onSchedule={() => setScheduleOpen(true)}
            />
          </TabPanel>

          <TabPanel value="activity">
            {activitiesQuery.isLoading ? (
              <Skeleton className="h-32 w-full" />
            ) : activitiesQuery.isError ? (
              <p className="text-sm text-text-muted">{t('applicationDetail.activity.error')}</p>
            ) : (activitiesQuery.data?.length ?? 0) === 0 ? (
              <p className="text-sm text-text-muted">{t('applicationDetail.activity.empty')}</p>
            ) : (
              <ActivityTimeline activities={activitiesQuery.data ?? []} stages={stagesQuery.data ?? []} />
            )}
          </TabPanel>
        </Tabs>
      </Card>

      <CorrectStageDialog
        open={correctStageOpen}
        onOpenChange={setCorrectStageOpen}
        stageOptions={(stagesQuery.data ?? []).filter(
          (stage) =>
            stage.id !== application.stageId && stage.type !== 'FinalHired' && stage.type !== 'FinalRejected',
        )}
        onConfirm={handleCorrectStage}
        submitting={correctStage.isPending}
      />

      <RejectDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        onConfirm={handleReject}
        submitting={reject.isPending}
      />

      <HireDialog
        open={hireOpen}
        onOpenChange={setHireOpen}
        onConfirm={handleHire}
        submitting={hire.isPending}
      />

      <ScheduleInterviewModal
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        applicationId={id}
        candidateName={application.candidateName}
        // Stay on the application and surface the new interview in its tab; the schedule mutation
        // invalidates the interviews cache, so the tab's list refetches on its own.
        onScheduled={() => setTab('interviews')}
      />
    </div>
  );
}
