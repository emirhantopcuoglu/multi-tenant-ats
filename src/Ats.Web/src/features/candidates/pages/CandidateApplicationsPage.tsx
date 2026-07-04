import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Pagination, Skeleton } from '@/components/ui';
import { applicationStatusTone } from '@/lib/statusColors';
import { PublicLayout } from '@/features/public/components/PublicLayout';
import { useCandidateApplications } from '../useCandidateApplications';

const PAGE_SIZE = 10;

export function CandidateApplicationsPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const query = useCandidateApplications(page, PAGE_SIZE);
  const items = query.data?.items ?? [];
  const totalPages = query.data?.totalPages ?? 1;

  const dateFormatter = new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  return (
    <PublicLayout>
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">
            {t('candidatePortal.applicationsTitle')}
          </h1>
          <p className="text-sm text-text-muted">{t('candidatePortal.applicationsSubtitle')}</p>
        </div>

        {query.isLoading ? (
          <div className="space-y-3" aria-busy="true">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        ) : query.isError ? (
          <EmptyState
            title={t('candidatePortal.loadError')}
            action={
              <button
                type="button"
                onClick={() => void query.refetch()}
                className="text-sm font-medium text-accent hover:underline"
              >
                {t('candidatePortal.retry')}
              </button>
            }
          />
        ) : items.length === 0 ? (
          <EmptyState
            title={t('candidatePortal.empty')}
            action={
              <Link to="/" className="text-sm font-medium text-accent hover:underline">
                {t('candidatePortal.exploreJobs')}
              </Link>
            }
          />
        ) : (
          <>
            <ul className="space-y-3">
              {items.map((item) => (
                <li key={item.id}>
                  <Card className="flex items-center justify-between gap-4 py-4">
                    <div className="min-w-0 flex-1 space-y-0.5">
                      <Link
                        to={`/${item.companySlug}/jobs/${item.jobSlug}`}
                        className="block truncate text-base font-medium text-text hover:text-accent"
                      >
                        {item.jobTitle}
                      </Link>
                      <p className="text-sm text-text-muted">{item.companyName}</p>
                    </div>

                    <div className="flex shrink-0 flex-col items-end gap-1.5 text-right">
                      <div className="flex items-center gap-2">
                        {item.currentStageName && (
                          <Badge tone="neutral">{item.currentStageName}</Badge>
                        )}
                        <Badge tone={applicationStatusTone[item.status]} dot>
                          {t(`status.${item.status}`)}
                        </Badge>
                      </div>
                      <span className="text-xs text-text-muted">
                        {dateFormatter.format(new Date(item.appliedAtUtc))}
                      </span>
                      <Link
                        to={`/candidate/applications/${item.id}`}
                        className="text-sm font-medium text-accent hover:underline"
                      >
                        {t('candidatePortal.tracking.detailLink')}
                      </Link>
                    </div>
                  </Card>
                </li>
              ))}
            </ul>

            {totalPages > 1 && (
              <div className="flex justify-center pt-2">
                <Pagination
                  page={page}
                  pageCount={totalPages}
                  onPageChange={(p) => {
                    setPage(p);
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                  }}
                />
              </div>
            )}
          </>
        )}
      </div>
    </PublicLayout>
  );
}
