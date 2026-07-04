import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Card, EmptyState, Skeleton } from '@/components/ui';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { usePublicCompany } from './usePublicCompany';
import { usePublicJobs } from './usePublicJobs';

/* Public careers page for a company, reached at /{slug}. The header is the company's public profile
   (name, description, website, location — edited in Settings); below it, the Published jobs as cards.
   Profile and jobs are separate queries against separate endpoints, but both 404 on an unknown slug,
   so either failing surfaces the not-found state. */
export function PublicCareersPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams();
  const companyQuery = usePublicCompany(slug);
  const jobsQuery = usePublicJobs(slug);

  if (companyQuery.isError || jobsQuery.isError) {
    return <PublicNotFound />;
  }

  if (companyQuery.isLoading || jobsQuery.isLoading) {
    return (
      <PublicLayout>
        <div className="space-y-8">
          <Skeleton className="h-24 w-full" />
          <div className="space-y-3">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        </div>
      </PublicLayout>
    );
  }

  const company = companyQuery.data;
  const jobs = jobsQuery.data?.items ?? [];

  return (
    <PublicLayout>
      <div className="space-y-8">
        <header className="space-y-3">
          <div className="space-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">{company?.companyName ?? slug}</h1>
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-text-muted">
              {company?.location && <span>{company.location}</span>}
              {company?.location && company?.website && <span aria-hidden="true">·</span>}
              {company?.website && (
                <a
                  href={company.website}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-accent hover:underline"
                >
                  {t('public.careers.website')}
                </a>
              )}
            </div>
          </div>
          {company?.description && (
            <p className="max-w-2xl whitespace-pre-line text-sm leading-relaxed text-text-muted">
              {company.description}
            </p>
          )}
        </header>

        <div className="space-y-4">
          <h2 className="text-lg font-semibold tracking-tight">
            {t('public.careers.title')}
            {company && company.openJobCount > 0 && (
              <span className="ml-2 text-sm font-normal text-text-muted">
                {t('public.careers.openCount', { count: company.openJobCount })}
              </span>
            )}
          </h2>

          {jobs.length === 0 ? (
            <EmptyState title={t('public.careers.empty')} />
          ) : (
            <ul className="space-y-3">
              {jobs.map((job) => (
                <li key={job.id}>
                  <Link to={`/${slug}/jobs/${job.slug}`} className="block">
                    <Card className="space-y-2 transition-colors hover:border-accent">
                      <h3 className="text-base font-semibold text-text">{job.title}</h3>
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
      </div>
    </PublicLayout>
  );
}
