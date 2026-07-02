import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Pagination } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { useUsers } from '@/features/users/useUsers';
import { canManageInterviews } from './interviewPermissions';
import { isDateRange, resolveDateRange, type DateRange } from './dateRange';
import { useInterviews } from './useInterviews';
import { InterviewsToolbar } from './components/InterviewsToolbar';
import { InterviewsTable, InterviewsTableSkeleton } from './components/InterviewsTable';
import { ScheduleInterviewModal } from './components/ScheduleInterviewModal';

const PAGE_SIZE = 20;

/* Interviews list (Step 3.6). A date-range preset and an interviewer filter (both mirrored to the URL)
   drive the query; managers can schedule from here. The calendar view is deferred to a later slice. */
export function InterviewsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { role } = useAuth();
  const canManage = canManageInterviews(role);
  const [searchParams, setSearchParams] = useSearchParams();
  const [scheduleOpen, setScheduleOpen] = useState(false);

  const pageParam = Number(searchParams.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;
  const rangeParam = searchParams.get('range');
  const range: DateRange = isDateRange(rangeParam) ? rangeParam : 'all';
  const interviewerId = searchParams.get('interviewer') ?? '';

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

  const setRange = (value: DateRange) =>
    updateParams((params) => {
      if (value === 'all') params.delete('range');
      else params.set('range', value);
      params.delete('page');
    });

  const setInterviewer = (value: string) =>
    updateParams((params) => {
      if (value) params.set('interviewer', value);
      else params.delete('interviewer');
      params.delete('page');
    });

  // Recompute bounds whenever the preset changes (and pin "now" per render via useMemo on range).
  const bounds = useMemo(() => resolveDateRange(range), [range]);

  const { data, isLoading, isError, refetch } = useInterviews({
    page,
    pageSize: PAGE_SIZE,
    fromDate: bounds.fromDate,
    toDate: bounds.toDate,
    interviewerId: interviewerId || undefined,
  });

  const usersQuery = useUsers();
  const interviews = data?.items ?? [];
  const hasFilters = range !== 'all' || Boolean(interviewerId);

  const openInterview = (id: string) => navigate(`/interviews/${id}`);

  return (
    <div className="space-y-5">
      <InterviewsToolbar
        range={range}
        onRangeChange={setRange}
        interviewerId={interviewerId}
        onInterviewerChange={setInterviewer}
        users={usersQuery.data ?? []}
        canManage={canManage}
        onSchedule={() => setScheduleOpen(true)}
      />

      {isLoading ? (
        <InterviewsTableSkeleton />
      ) : isError ? (
        <Card>
          <EmptyState
            title={t('interviews.loadError')}
            action={
              <Button variant="secondary" onClick={() => refetch()}>
                {t('interviews.retry')}
              </Button>
            }
          />
        </Card>
      ) : interviews.length === 0 ? (
        <Card>
          <EmptyState
            title={t(hasFilters ? 'interviews.empty.filtered' : 'interviews.empty.title')}
            description={hasFilters ? undefined : t('interviews.empty.body')}
          />
        </Card>
      ) : (
        <div className="space-y-4">
          <InterviewsTable interviews={interviews} onSelect={openInterview} />
          <div className="flex flex-col items-center justify-between gap-3 sm:flex-row">
            <p className="text-sm text-text-muted">
              {t('interviews.rows', { count: data?.totalCount ?? 0 })}
            </p>
            <Pagination
              page={page}
              pageCount={data?.totalPages ?? 1}
              onPageChange={(nextPage) => updateParams((params) => params.set('page', String(nextPage)))}
            />
          </div>
        </div>
      )}

      <ScheduleInterviewModal
        open={scheduleOpen}
        onOpenChange={setScheduleOpen}
        onScheduled={openInterview}
      />
    </div>
  );
}
