import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Pagination, useToast } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { JOB_STATUSES, type JobStatus } from '@/types/enums';
import type { Job } from '@/types/job';
import { canManageJobs } from './jobPermissions';
import { useJobActions, useJobs } from './useJobs';
import { JobsToolbar } from './components/JobsToolbar';
import { JobsTable, JobsTableSkeleton } from './components/JobsTable';
import type { JobAction } from './components/JobRowActions';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 350;

/* Recruiter Jobs list (Step 3.1). Filters and the page number live in the URL (?status&q&page) so the
   view is shareable and survives reload; the search box is debounced into the URL to avoid a request
   per keystroke. The four list states (loading / error / empty / data) are each handled explicitly. */
export function JobsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { toast } = useToast();
  const { role, user } = useAuth();
  const canManage = canManageJobs(role);

  const [searchParams, setSearchParams] = useSearchParams();
  const pageParam = Number(searchParams.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;
  const statusParam = searchParams.get('status');
  const status: JobStatus | '' = JOB_STATUSES.includes(statusParam as JobStatus)
    ? (statusParam as JobStatus)
    : '';
  const search = searchParams.get('q') ?? '';

  const setParam = (key: 'page' | 'status' | 'q', value: string) => {
    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous);
        if (value) next.set(key, value);
        else next.delete(key);
        // Any filter change returns to the first page; otherwise page 2 of a no-longer-matching filter.
        if (key !== 'page') next.delete('page');
        return next;
      },
      { replace: true },
    );
  };

  // Local search text, committed to the URL after a pause so typing stays responsive.
  const [searchInput, setSearchInput] = useState(search);
  useEffect(() => {
    const handle = setTimeout(() => {
      if (searchInput.trim() !== search) setParam('q', searchInput.trim());
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
    // Re-run when the user types (searchInput) or the URL settles (search); setParam reads fresh state.
  }, [searchInput, search]);

  const { data, isLoading, isError, refetch } = useJobs({
    page,
    pageSize: PAGE_SIZE,
    status: status || undefined,
    search: search || undefined,
  });

  const { publish, close, archive } = useJobActions();

  const handleAction = (job: Job, action: JobAction) => {
    if (action === 'edit') {
      navigate(`/jobs/${job.id}/edit`);
      return;
    }
    const mutation = action === 'publish' ? publish : action === 'close' ? close : archive;
    const successKey =
      action === 'publish'
        ? 'jobs.toast.published'
        : action === 'close'
          ? 'jobs.toast.closed'
          : 'jobs.toast.archived';
    mutation.mutate(job.id, {
      onSuccess: () => toast({ title: t(successKey), tone: 'success' }),
      onError: () => toast({ title: t('jobs.toast.error'), tone: 'danger' }),
    });
  };

  const jobs = data?.items ?? [];
  const hasFilters = status !== '' || search.trim() !== '';

  return (
    <div className="space-y-5">
      <JobsToolbar
        searchValue={searchInput}
        onSearchChange={setSearchInput}
        status={status}
        onStatusChange={(value) => setParam('status', value)}
        canManage={canManage}
        onNewJob={() => navigate('/jobs/new')}
        careersSlug={user?.kind === 'company' ? user.tenant.slug : undefined}
      />

      {isLoading ? (
        <JobsTableSkeleton canManage={canManage} />
      ) : isError ? (
        <Card>
          <EmptyState
            title={t('jobs.loadError')}
            action={
              <Button variant="secondary" onClick={() => refetch()}>
                {t('jobs.retry')}
              </Button>
            }
          />
        </Card>
      ) : jobs.length === 0 ? (
        <Card>
          <EmptyState
            title={t(hasFilters ? 'jobs.empty.filtered' : 'jobs.empty.title')}
            description={hasFilters ? undefined : t('jobs.empty.body')}
            action={
              canManage && !hasFilters ? (
                <Button onClick={() => navigate('/jobs/new')}>{t('jobs.newJob')}</Button>
              ) : undefined
            }
          />
        </Card>
      ) : (
        <div className="space-y-4">
          <JobsTable jobs={jobs} canManage={canManage} onAction={handleAction} />
          <div className="flex flex-col items-center justify-between gap-3 sm:flex-row">
            <p className="text-sm text-text-muted">
              {t('jobs.rows', { count: data?.totalCount ?? 0 })}
            </p>
            <Pagination
              page={page}
              pageCount={data?.totalPages ?? 1}
              onPageChange={(nextPage) => setParam('page', String(nextPage))}
            />
          </div>
        </div>
      )}
    </div>
  );
}
