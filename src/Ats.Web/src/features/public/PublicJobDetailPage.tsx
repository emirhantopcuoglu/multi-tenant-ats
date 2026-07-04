import { Link, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Avatar, Badge, Button, Card, Skeleton } from '@/components/ui';
import { Markdown } from '@/components/Markdown';
import { useAuth } from '@/app/auth/auth-context';
import { useAppliedJobIds } from '@/features/candidates/useAppliedJobIds';
import type { Job } from '@/types/job';
import type { PublicCompanyProfile } from './publicCompanyApi';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { usePublicCompany } from './usePublicCompany';
import { usePublicJob, usePublicJobs } from './usePublicJobs';

const OTHER_JOBS_LIMIT = 3;

/* Public job detail at /{slug}/jobs/{jobSlug}: full markdown description, salary, posting date, a
   company card linking to the careers page, and up to three other open roles from the same company.
   A signed-in candidate who already has an active application sees an "applied" state instead of
   the Apply CTA. An unknown or unpublished job resolves to a 404 state (the backend returns 404,
   never a stub). The company profile and sibling-jobs queries only enrich the page — if either is
   still loading or failed, their sections are simply absent; the job itself decides 404. */
export function PublicJobDetailPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { slug = '', jobSlug = '' } = useParams();
  const { user } = useAuth();
  const jobQuery = usePublicJob(slug, jobSlug);
  const companyQuery = usePublicCompany(slug);
  const jobsQuery = usePublicJobs(slug);
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
  const company = companyQuery.data;
  const salary = formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryCurrency, i18n.language);
  const postedOn = job.publishedAtUtc
    ? new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long' }).format(
        new Date(job.publishedAtUtc),
      )
    : null;
  const otherJobs = (jobsQuery.data?.items ?? [])
    .filter((item) => item.id !== job.id)
    .slice(0, OTHER_JOBS_LIMIT);

  return (
    <PublicLayout>
      <div className="space-y-6">
        <Link to={`/${slug}`} className="text-sm text-text-muted transition-colors hover:text-accent">
          {t('public.detail.back')}
        </Link>

        <div className="space-y-2">
          {company && (
            <Link
              to={`/${slug}`}
              className="text-sm font-medium text-text-muted transition-colors hover:text-accent"
            >
              {company.companyName}
            </Link>
          )}
          <h1 className="text-2xl font-semibold tracking-tight">{job.title}</h1>
          <div className="flex flex-wrap items-center gap-2 text-sm text-text-muted">
            <span>{job.department}</span>
            <span aria-hidden="true">·</span>
            <span>{job.location}</span>
            {postedOn && (
              <>
                <span aria-hidden="true">·</span>
                <span>{t('public.detail.postedOn', { date: postedOn })}</span>
              </>
            )}
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

        {company && <CompanyCard slug={slug} company={company} />}

        {otherJobs.length > 0 && (
          <section className="space-y-3" aria-labelledby="other-jobs-heading">
            <h2 id="other-jobs-heading" className="text-lg font-semibold tracking-tight">
              {company
                ? t('public.detail.otherJobsAt', { name: company.companyName })
                : t('public.detail.otherJobs')}
            </h2>
            <ul className="space-y-3">
              {otherJobs.map((item) => (
                <OtherJobRow key={item.id} slug={slug} job={item} />
              ))}
            </ul>
            <Link
              to={`/${slug}`}
              className="inline-block text-sm font-medium text-accent hover:underline"
            >
              {t('public.detail.allJobs')}
            </Link>
          </section>
        )}
      </div>
    </PublicLayout>
  );
}

/* The "who is hiring" card: initials avatar (no logo upload yet), name and location, the public
   description, and a link back to the careers page. Everything but the name is optional — the card
   collapses gracefully when the company never filled its profile in. */
function CompanyCard({ slug, company }: { slug: string; company: PublicCompanyProfile }) {
  const { t } = useTranslation();

  return (
    <Card className="space-y-3">
      <div className="flex items-center gap-3">
        <Avatar name={company.companyName} size="lg" className="h-12 w-12 rounded-xl text-base" />
        <div className="min-w-0">
          <Link
            to={`/${slug}`}
            className="block truncate text-base font-semibold text-text transition-colors hover:text-accent"
          >
            {company.companyName}
          </Link>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-text-muted">
            {company.location && <span>{company.location}</span>}
            {company.location && company.website && <span aria-hidden="true">·</span>}
            {company.website && (
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
      </div>
      {company.description && (
        <p className="line-clamp-3 whitespace-pre-line text-sm leading-relaxed text-text-muted">
          {company.description}
        </p>
      )}
      <Link to={`/${slug}`} className="inline-block text-sm font-medium text-accent hover:underline">
        {company.openJobCount > 0
          ? t('public.careers.openCount', { count: company.openJobCount })
          : t('public.detail.viewCompany')}
      </Link>
    </Card>
  );
}

/* One sibling role, rendered with the same shape as the careers-page cards so the two lists read
   as the same thing. */
function OtherJobRow({ slug, job }: { slug: string; job: Job }) {
  const { t } = useTranslation();

  return (
    <li>
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
