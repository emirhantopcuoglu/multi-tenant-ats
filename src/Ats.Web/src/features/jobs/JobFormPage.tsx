import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Skeleton, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { canManageJobs } from './jobPermissions';
import { useJob, useJobActions, useJobForm } from './useJobs';
import { JobForm } from './components/JobForm';
import {
  emptyJobValues,
  jobToValues,
  valuesToRequest,
  type JobFormValues,
} from './jobFormSchema';

/* Create (/jobs/new) and edit (/jobs/:id/edit) job form. Managing jobs is gated to Admin/Recruiter,
   so a non-manager who reaches the URL directly is redirected back to the list (the API enforces the
   same policy). Edit waits for the detail load before mounting the form so its defaults are correct. */
export function JobFormPage() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { toast } = useToast();
  const { role } = useAuth();

  const { data: job, isLoading, isError } = useJob(id);
  const { create, update } = useJobForm();
  const { publish } = useJobActions();

  if (!canManageJobs(role)) return <Navigate to="/jobs" replace />;

  if (isEdit && isLoading) {
    return (
      <Card className="mx-auto max-w-2xl">
        <Skeleton className="h-72 w-full" />
      </Card>
    );
  }

  if (isEdit && (isError || !job)) {
    return (
      <Card className="mx-auto max-w-2xl space-y-3 text-center">
        <p className="text-sm text-text-muted">{t('jobForm.loadError')}</p>
        <Button variant="secondary" onClick={() => navigate('/jobs')}>
          {t('common.cancel')}
        </Button>
      </Card>
    );
  }

  const defaultValues = isEdit && job ? jobToValues(job) : emptyJobValues();
  // Publishing only applies when creating or when the job is still a draft.
  const showPublish = !isEdit || job?.status === 'Draft';
  const submitting = create.isPending || update.isPending || publish.isPending;

  const handleSubmit = async (values: JobFormValues, doPublish: boolean) => {
    const body = valuesToRequest(values);
    try {
      const jobId = isEdit ? (id as string) : await create.mutateAsync(body);
      if (isEdit) await update.mutateAsync({ id: jobId, body });
      if (doPublish) await publish.mutateAsync(jobId);
      toast({ title: t(doPublish ? 'jobForm.publishedToast' : 'jobForm.savedToast'), tone: 'success' });
      navigate('/jobs');
    } catch {
      toast({ title: t('jobs.toast.error'), tone: 'danger' });
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <h2 className="text-lg font-semibold text-text">
        {isEdit ? t('jobForm.editTitle') : t('jobForm.createTitle')}
      </h2>
      <JobForm
        defaultValues={defaultValues}
        mode={isEdit ? 'edit' : 'create'}
        showPublish={showPublish}
        submitting={submitting}
        onSubmit={handleSubmit}
        onCancel={() => navigate('/jobs')}
      />
    </div>
  );
}
