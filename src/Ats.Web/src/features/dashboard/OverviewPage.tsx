import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Skeleton, StatCard } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { applicationStatusTone } from '@/lib/statusColors';
import { useApplications } from '@/features/applications/useApplications';
import { useInterviews } from '@/features/interviews/useInterviews';
import { useDashboardStats } from './useDashboardStats';

const PREVIEW_PAGE_SIZE = 5;

/* Dashboard home (Step 4.1). A row of four headline stats over two preview lists — recent
   applications and upcoming interviews — each reusing the list hooks from their own features. */
export function OverviewPage() {
  const { t } = useTranslation();
  const { user } = useAuth();

  const statsQuery = useDashboardStats();
  const recentApplications = useApplications({ page: 1, pageSize: PREVIEW_PAGE_SIZE });
  // "Upcoming" = scheduled from now on; pin the boundary once per mount so the query key is stable.
  const nowIso = useMemo(() => new Date().toISOString(), []);
  const upcomingInterviews = useInterviews({ page: 1, pageSize: PREVIEW_PAGE_SIZE, fromDate: nowIso });

  const stats = statsQuery.data;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-text">
          {t('overview.greeting', { name: user?.firstName ?? '' })}
        </h1>
        <p className="text-sm text-text-muted">{t('overview.summary')}</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label={t('overview.openJobs')}
          value={statsQuery.isLoading ? <Skeleton className="h-7 w-10" /> : (stats?.openJobs ?? '—')}
        />
        <StatCard
          label={t('overview.newApps')}
          hint={t('overview.thisWeek')}
          value={statsQuery.isLoading ? <Skeleton className="h-7 w-10" /> : (stats?.newApplicationsThisWeek ?? '—')}
        />
        <StatCard
          label={t('overview.upcomingInterviews')}
          value={statsQuery.isLoading ? <Skeleton className="h-7 w-10" /> : (stats?.upcomingInterviews ?? '—')}
        />
        <StatCard
          label={t('overview.activeCandidates')}
          value={statsQuery.isLoading ? <Skeleton className="h-7 w-10" /> : (stats?.activeCandidates ?? '—')}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <RecentApplications query={recentApplications} />
        <UpcomingInterviews query={upcomingInterviews} />
      </div>
    </div>
  );
}

function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card className="space-y-3">
      <h2 className="text-sm font-semibold text-text">{title}</h2>
      {children}
    </Card>
  );
}

function RecentApplications({ query }: { query: ReturnType<typeof useApplications> }) {
  const { t, i18n } = useTranslation();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });
  const applications = query.data?.items ?? [];

  return (
    <SectionCard title={t('overview.recentApps')}>
      {query.isLoading ? (
        <Skeleton className="h-24 w-full" />
      ) : applications.length === 0 ? (
        <EmptyState title={t('overview.noApps')} />
      ) : (
        <ul className="divide-y divide-divider">
          {applications.map((application) => (
            <li key={application.id}>
              <Link
                to={`/applications/${application.id}`}
                className="flex items-center justify-between gap-3 py-2.5 transition-colors hover:text-accent"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-text">{application.candidateName}</p>
                  <p className="text-xs text-text-muted">
                    {dateFormatter.format(new Date(application.appliedAtUtc))}
                  </p>
                </div>
                <Badge tone={applicationStatusTone[application.status]} dot>
                  {t(`status.${application.status}`)}
                </Badge>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}

function UpcomingInterviews({ query }: { query: ReturnType<typeof useInterviews> }) {
  const { t, i18n } = useTranslation();
  const dateTimeFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' });
  const interviews = query.data?.items ?? [];

  return (
    <SectionCard title={t('overview.upcomingInt')}>
      {query.isLoading ? (
        <Skeleton className="h-24 w-full" />
      ) : interviews.length === 0 ? (
        <EmptyState title={t('overview.noInterviews')} />
      ) : (
        <ul className="divide-y divide-divider">
          {interviews.map((interview) => (
            <li key={interview.id}>
              <Link
                to={`/interviews/${interview.id}`}
                className="flex items-center justify-between gap-3 py-2.5 transition-colors hover:text-accent"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-text">{interview.candidateName || '—'}</p>
                  <p className="text-xs text-text-muted">
                    {dateTimeFormatter.format(new Date(interview.scheduledAtUtc))}
                  </p>
                </div>
                <Badge tone="neutral">{t(`interviewType.${interview.type}`)}</Badge>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}
