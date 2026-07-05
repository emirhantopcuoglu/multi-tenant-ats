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
  const { move, reject } = useApplicationActions(id);

  const [tab, setTab] = useState('cv');
  const [rejectOpen, setRejectOpen] = useState(false);
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

  const busy = move.isPending || reject.isPending;

  const handleMove = (stageId: string) =>
    move.mutate(stageId, {
      onSuccess: () => toast({ title: t('applicationDetail.toast.moved'), tone: 'success' }),
      onError: () => toast({ title: t('applicationDetail.toast.error'), tone: 'danger' }),
    });

  const handleReject = (reason: string) =>
    reject.mutate(reason, {
      onSuccess: () => {
        setRejectOpen(false);
        toast({ title: t('applicationDetail.toast.rejected'), tone: 'success' });
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

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="flex items-center justify-between gap-3">
        <Link to="/applications" className="text-sm text-text-muted transition-colors hover:text-text">
          ← {t('applicationDetail.back')}
        </Link>
        {canScheduleInterview && application.status === 'Active' && (
          <Button variant="secondary" onClick={() => setScheduleOpen(true)}>
            {t('interviews.schedule')}
          </Button>
        )}
      </div>

      <ApplicationHeader
        application={application}
        stages={stagesQuery.data ?? []}
        canManage={canManage}
        onMove={handleMove}
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
            <ApplicationInterviewsTab applicationId={id} />
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

      <RejectDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        onConfirm={handleReject}
        submitting={reject.isPending}
      />

      <ScheduleInterviewModal
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        applicationId={id}
        candidateName={application.candidateName}
        onScheduled={(interviewId) => navigate(`/interviews/${interviewId}`)}
      />
    </div>
  );
}
