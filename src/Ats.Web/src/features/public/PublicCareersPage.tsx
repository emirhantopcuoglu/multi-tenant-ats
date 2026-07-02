import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Skeleton } from '@/components/ui';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { usePublicJobs } from './usePublicJobs';

/* Public careers list for a tenant, reached at /{slug}. Lists the tenant's Published jobs as cards
   that link to the detail page. An unresolved slug surfaces as a 404 state (the backend returns 404,
   not an empty 200). The company name isn't exposed by the public list endpoint yet, so the heading
   stays generic rather than inventing one. */
export function PublicCareersPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams();
  const jobsQuery = usePublicJobs(slug);

  if (jobsQuery.isError) {
    return <PublicNotFound />;
  }

  const jobs = jobsQuery.data?.items ?? [];

  return (
    <PublicLayout>
      <div className="space-y-8">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('public.careers.title')}</h1>
          <p className="text-sm text-text-muted">{slug}</p>
        </div>

        {jobsQuery.isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : jobs.length === 0 ? (
          <EmptyState title={t('public.careers.empty')} />
        ) : (
          <ul className="space-y-3">
            {jobs.map((job) => (
              <li key={job.id}>
                <Link to={`/${slug}/jobs/${job.slug}`} className="block">
                  <Card className="space-y-2 transition-colors hover:border-accent">
                    <h2 className="text-base font-semibold text-text">{job.title}</h2>
                    <div className="flex flex-wrap items-center gap-2 text-sm text-text-muted">
                      <span>{job.department}</span>
                      <span aria-hidden="true">·</span>
                      <span>{job.location}</span>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge tone="neutral">{t(`employmentType.${job.employmentType}`)}</Badge>
                      <Badge tone="neutral">{t(`experienceLevel.${job.experienceLevel}`)}</Badge>
                    </div>
                  </Card>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </PublicLayout>
  );
}
