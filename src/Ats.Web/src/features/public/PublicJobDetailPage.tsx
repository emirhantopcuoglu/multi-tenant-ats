import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Badge, Button, Card, Skeleton } from '@/components/ui';
import { Markdown } from '@/components/Markdown';
import { useAuth } from '@/app/auth/auth-context';
import { useAppliedJobIds } from '@/features/candidates/useAppliedJobIds';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { usePublicJob } from './usePublicJobs';

/* Public job detail at /{slug}/jobs/{jobSlug}: full markdown description, salary, meta, and the Apply
   CTA. A signed-in candidate who already has an active application sees an "applied" state instead
   of the CTA. An unknown or unpublished job resolves to a 404 state (the backend returns 404,
   never a stub). */
export function PublicJobDetailPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { slug = '', jobSlug = '' } = useParams();
  const { user } = useAuth();
  const jobQuery = usePublicJob(slug, jobSlug);
  const appliedJobIds = useAppliedJobIds(user?.kind === 'candidate');

  if (jobQuery.isLoading) {
    return (
      <PublicLayout>
        <div className="space-y-4">
          <Skeleton className="h-8 w-2/3" />
          <Skeleton className="h-64 w-full" />
        </div>
      </PublicLayout>
    );
  }

  if (jobQuery.isError || !jobQuery.data) {
    return <PublicNotFound />;
  }

  const job = jobQuery.data;
  const salary = formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryCurrency, i18n.language);

  return (
    <PublicLayout>
      <div className="space-y-6">
        <Link to={`/${slug}`} className="text-sm text-text-muted transition-colors hover:text-accent">
          {t('public.detail.back')}
        </Link>

        <div className="space-y-2">
          <h1 className="text-2xl font-semibold tracking-tight">{job.title}</h1>
          <div className="flex flex-wrap items-center gap-2 text-sm text-text-muted">
            <span>{job.department}</span>
            <span aria-hidden="true">·</span>
            <span>{job.location}</span>
          </div>
          <div className="flex flex-wrap gap-2 pt-1">
            <Badge tone="neutral">{t(`employmentType.${job.employmentType}`)}</Badge>
            <Badge tone="neutral">{t(`experienceLevel.${job.experienceLevel}`)}</Badge>
            {salary && <Badge tone="accent">{salary}</Badge>}
          </div>
        </div>

        <Card>
          <Markdown>{job.description}</Markdown>
        </Card>

        <div className="flex justify-center">
          {appliedJobIds.data?.has(job.id) ? (
            <div className="flex flex-col items-center gap-2">
              <Badge tone="success" dot>
                {t('public.detail.applied')}
              </Badge>
              <Link
                to="/candidate/applications"
                className="text-sm font-medium text-accent hover:underline"
              >
                {t('public.detail.viewApplications')}
              </Link>
            </div>
          ) : (
            <Button onClick={() => navigate(`/${slug}/jobs/${jobSlug}/apply`)}>
              {t('public.detail.apply')}
            </Button>
          )}
        </div>
      </div>
    </PublicLayout>
  );
}

/* Render a salary range as a localized currency string, or null when no salary was set. Wrapped in a
   try/catch because Intl throws on an unrecognized currency code; we fall back to a plain rendering
   rather than crash the page on bad data. */
function formatSalaryRange(
  min: number | null,
  max: number | null,
  currency: string | null,
  locale: string,
): string | null {
  if (min === null || max === null || !currency) return null;

  try {
    const formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    });
    return `${formatter.format(min)} – ${formatter.format(max)}`;
  } catch {
    return `${min.toLocaleString(locale)} – ${max.toLocaleString(locale)} ${currency}`;
  }
}
