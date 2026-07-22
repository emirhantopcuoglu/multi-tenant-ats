import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Pagination, Select, Skeleton } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { APPLICATION_STATUSES, type ApplicationStatus } from '@/types/enums';
import { canManageApplications } from './applicationPermissions';
import { useApplications, useJobOptions, useJobStages } from './useApplications';
import { useApplicationsBoard } from './useApplicationsBoard';
import { ApplicationsToolbar } from './components/ApplicationsToolbar';
import { ApplicationsTable, ApplicationsTableSkeleton } from './components/ApplicationsTable';
import { ApplicationsBoard } from './components/ApplicationsBoard';
import { ScheduleInterviewModal } from '@/features/interviews/components/ScheduleInterviewModal';

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 350;

const VIEWS = ['table', 'board'] as const;
type ApplicationsView = (typeof VIEWS)[number];

function ViewToggle({ view, onChange }: { view: ApplicationsView; onChange: (view: ApplicationsView) => void }) {
  const { t } = useTranslation();
  return (
    <div role="group" aria-label={t('applications.view.label')} className="flex gap-0.5 rounded-lg border border-border bg-bg p-0.5">
      {VIEWS.map((value) => (
        <button
          key={value}
          type="button"
          onClick={() => onChange(value)}
          aria-pressed={view === value}
          className={
            'rounded-md px-3 py-1 text-xs font-semibold transition-colors ' +
            (view === value ? 'bg-card text-accent shadow-card' : 'text-text-muted hover:text-text')
          }
        >
          {t(`applications.view.${value}`)}
        </button>
      ))}
    </div>
  );
}

/* Recruiter applications screen (Steps 3.3 + 3.4). A view toggle switches between the filtered table
   and the per-job Kanban board; filters, page, and the active view all live in the URL. */
export function ApplicationsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { role } = useAuth();
  const canManage = canManageApplications(role);
  const [searchParams, setSearchParams] = useSearchParams();

  const openApplication = (id: string) => navigate(`/applications/${id}`);

  const pageParam = Number(searchParams.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;
  const jobId = searchParams.get('job') ?? '';
  const stageId = searchParams.get('stage') ?? '';
  const statusParam = searchParams.get('status');
  const status: ApplicationStatus | '' = APPLICATION_STATUSES.includes(statusParam as ApplicationStatus)
    ? (statusParam as ApplicationStatus)
    : '';
  const search = searchParams.get('q') ?? '';
  const view: ApplicationsView = searchParams.get('view') === 'board' ? 'board' : 'table';

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
      params.delete('page');
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

  const setView = (value: ApplicationsView) =>
    updateParams((params) => {
      if (value === 'table') params.delete('view');
      else params.set('view', value);
      params.delete('page');
    });

  // Dropping a board card onto the Interview column opens this instead of moving it directly — the
  // move itself happens server-side once the interview is actually scheduled.
  const [interviewPrompt, setInterviewPrompt] = useState<{ applicationId: string; candidateName: string } | null>(
    null,
  );

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
  const board = useApplicationsBoard(view === 'board' ? jobId || undefined : undefined);

  const jobs = useMemo(() => jobOptions.data?.items ?? [], [jobOptions.data]);
  const jobTitleById = useMemo(() => new Map(jobs.map((job) => [job.id, job.title])), [jobs]);
  const jobTitleOf = (id: string) => jobTitleById.get(id) ?? '—';

  const applications = data?.items ?? [];
  const hasFilters = Boolean(jobId || stageId || status || search.trim());

  return (
    <div className="space-y-5">
      <div className="flex justify-end">
        <ViewToggle view={view} onChange={setView} />
      </div>

      {view === 'table' ? (
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
              <ApplicationsTable applications={applications} jobTitleOf={jobTitleOf} onSelect={openApplication} />
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
      ) : (
        <div className="space-y-4">
          <Select
            aria-label={t('applications.filterJob')}
            value={jobId}
            onChange={(event) => setJob(event.target.value)}
            className="sm:w-64"
          >
            <option value="">{t('applications.allJobs')}</option>
            {jobs.map((job) => (
              <option key={job.id} value={job.id}>
                {job.title}
              </option>
            ))}
          </Select>

          {!jobId ? (
            <Card>
              <EmptyState title={t('applications.selectJob.title')} description={t('applications.selectJob.body')} />
            </Card>
          ) : board.applicationsQuery.isLoading || board.stagesQuery.isLoading ? (
            <Card>
              <Skeleton className="h-64 w-full" />
            </Card>
          ) : board.applicationsQuery.isError || board.stagesQuery.isError ? (
            <Card>
              <EmptyState
                title={t('applications.loadError')}
                action={
                  <Button
                    variant="secondary"
                    onClick={() => {
                      board.applicationsQuery.refetch();
                      board.stagesQuery.refetch();
                    }}
                  >
                    {t('applications.retry')}
                  </Button>
                }
              />
            </Card>
          ) : (
            <ApplicationsBoard
              stages={board.stagesQuery.data ?? []}
              applications={board.applicationsQuery.data?.items ?? []}
              canManage={canManage}
              onMove={(id, targetStageId) => board.move.mutate({ id, targetStageId })}
              onScheduleInterview={(applicationId, candidateName) =>
                setInterviewPrompt({ applicationId, candidateName })
              }
              onSelect={openApplication}
            />
          )}
        </div>
      )}

      <ScheduleInterviewModal
        open={interviewPrompt !== null}
        onOpenChange={(open) => {
          if (!open) setInterviewPrompt(null);
        }}
        applicationId={interviewPrompt?.applicationId}
        candidateName={interviewPrompt?.candidateName}
        onScheduled={() => board.applicationsQuery.refetch()}
      />
    </div>
  );
}
