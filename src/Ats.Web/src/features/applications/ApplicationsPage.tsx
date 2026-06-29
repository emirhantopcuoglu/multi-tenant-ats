import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Pagination } from '@/components/ui';
import { APPLICATION_STATUSES, type ApplicationStatus } from '@/types/enums';
import { useApplications, useJobOptions, useJobStages } from './useApplications';
import { ApplicationsToolbar } from './components/ApplicationsToolbar';
import { ApplicationsTable, ApplicationsTableSkeleton } from './components/ApplicationsTable';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 350;

/* Recruiter applications list (Step 3.3). Filters (job, stage, status, candidate search) and the page
   live in the URL so the view is shareable and survives reload. Stages are per-job, so the stage
   filter is enabled only once a job is chosen and is cleared whenever the job changes. */
export function ApplicationsPage() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();

  const pageParam = Number(searchParams.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;
  const jobId = searchParams.get('job') ?? '';
  const stageId = searchParams.get('stage') ?? '';
  const statusParam = searchParams.get('status');
  const status: ApplicationStatus | '' = APPLICATION_STATUSES.includes(statusParam as ApplicationStatus)
    ? (statusParam as ApplicationStatus)
    : '';
  const search = searchParams.get('q') ?? '';

  const updateParams = (mutate: (params: URLSearchParams) => void) => {
    setSearchParams(
      (previous) => {
        const next = new URLSearchParams(previous);
        mutate(next);
        return next;
      },
      { replace: true },
    );
  };

  const setFilter = (key: 'status' | 'q', value: string) =>
    updateParams((params) => {
      if (value) params.set(key, value);
      else params.delete(key);
      params.delete('page'); // a filter change returns to page 1
    });

  const setStage = (value: string) =>
    updateParams((params) => {
      if (value) params.set('stage', value);
      else params.delete('stage');
      params.delete('page');
    });

  // Changing the job clears the stage filter — a stage id belongs to the old job's pipeline.
  const setJob = (value: string) =>
    updateParams((params) => {
      if (value) params.set('job', value);
      else params.delete('job');
      params.delete('stage');
      params.delete('page');
    });

  const [searchInput, setSearchInput] = useState(search);
  useEffect(() => {
    const handle = setTimeout(() => {
      if (searchInput.trim() !== search) setFilter('q', searchInput.trim());
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
    // Re-run when the user types or the URL settles.
  }, [searchInput, search]);

  const { data, isLoading, isError, refetch } = useApplications({
    page,
    pageSize: PAGE_SIZE,
    jobId: jobId || undefined,
    stageId: stageId || undefined,
    status: status || undefined,
    search: search || undefined,
  });

  const jobOptions = useJobOptions();
  const stagesQuery = useJobStages(jobId || undefined);

  const jobs = useMemo(() => jobOptions.data?.items ?? [], [jobOptions.data]);
  const jobTitleById = useMemo(() => new Map(jobs.map((job) => [job.id, job.title])), [jobs]);
  const jobTitleOf = (id: string) => jobTitleById.get(id) ?? '—';

  const applications = data?.items ?? [];
  const hasFilters = Boolean(jobId || stageId || status || search.trim());

  return (
    <div className="space-y-5">
      <ApplicationsToolbar
        searchValue={searchInput}
        onSearchChange={setSearchInput}
        jobId={jobId}
        onJobChange={setJob}
        stageId={stageId}
        onStageChange={setStage}
        status={status}
        onStatusChange={(value) => setFilter('status', value)}
        jobs={jobs}
        stages={stagesQuery.data ?? []}
        stagesEnabled={Boolean(jobId)}
      />

      {isLoading ? (
        <ApplicationsTableSkeleton />
      ) : isError ? (
        <Card>
          <EmptyState
            title={t('applications.loadError')}
            action={
              <Button variant="secondary" onClick={() => refetch()}>
                {t('applications.retry')}
              </Button>
            }
          />
        </Card>
      ) : applications.length === 0 ? (
        <Card>
          <EmptyState
            title={t(hasFilters ? 'applications.empty.filtered' : 'applications.empty.title')}
            description={hasFilters ? undefined : t('applications.empty.body')}
          />
        </Card>
      ) : (
        <div className="space-y-4">
          <ApplicationsTable applications={applications} jobTitleOf={jobTitleOf} />
          <div className="flex flex-col items-center justify-between gap-3 sm:flex-row">
            <p className="text-sm text-text-muted">
              {t('applications.rows', { count: data?.totalCount ?? 0 })}
            </p>
            <Pagination
              page={page}
              pageCount={data?.totalPages ?? 1}
              onPageChange={(nextPage) => updateParams((params) => params.set('page', String(nextPage)))}
            />
          </div>
        </div>
      )}
    </div>
  );
}
