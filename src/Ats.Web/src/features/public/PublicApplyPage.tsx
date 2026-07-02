import { useState } from 'react';
import { Link, Navigate, useLocation, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Field, Input, Skeleton, Textarea } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { toApiError } from '@/lib/problemDetails';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { CvUpload } from './components/CvUpload';
import { usePublicJob } from './usePublicJobs';
import { useApplyToJob } from './useApplyToJob';
import { validateCvFile, type CvFileError } from './cvFile';

const FILE_ERROR_KEYS = {
  'file.empty': 'public.apply.fileEmpty',
  'file.too_large': 'public.apply.fileTooLarge',
  'file.unsupported_type': 'public.apply.fileUnsupported',
  'file.content_mismatch': 'public.apply.fileMismatch',
} as const satisfies Record<CvFileError | 'file.content_mismatch', string>;

export function PublicApplyPage() {
  const { t } = useTranslation();
  const { slug = '', jobSlug = '' } = useParams();
  const location = useLocation();
  const { user, isLoading: authLoading } = useAuth();
  const jobQuery = usePublicJob(slug, jobSlug);
  const apply = useApplyToJob(slug, jobSlug);

  const [phone, setPhone] = useState('');
  const [linkedInUrl, setLinkedInUrl] = useState('');
  const [coverLetter, setCoverLetter] = useState('');
  const [cv, setCv] = useState<File | null>(null);
  const [cvError, setCvError] = useState<string | undefined>(undefined);
  const [bannerError, setBannerError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  if (authLoading || jobQuery.isLoading) {
    return (
      <PublicLayout>
        <Skeleton className="h-64 w-full" />
      </PublicLayout>
    );
  }

  if (jobQuery.isError || !jobQuery.data) {
    return <PublicNotFound />;
  }

  // Must be a signed-in candidate to apply. Redirect to candidate login, preserving the return URL.
  if (!user || user.kind !== 'candidate') {
    return <Navigate to="/candidate/login" replace state={{ from: location }} />;
  }

  const job = jobQuery.data;

  const selectCv = (file: File) => {
    const code = validateCvFile(file);
    if (code) {
      setCv(null);
      setCvError(t(FILE_ERROR_KEYS[code]));
      return;
    }
    setCv(file);
    setCvError(undefined);
  };

  const handleSubmit = () => {
    setBannerError(null);
    if (!cv) {
      setCvError(t('public.apply.cvRequired'));
      return;
    }
    setCvError(undefined);

    apply.mutate(
      {
        phone: phone.trim() || undefined,
        linkedInUrl: linkedInUrl.trim() || undefined,
        coverLetter: coverLetter.trim() || undefined,
        cv,
      },
      {
        onSuccess: () => setSubmitted(true),
        onError: (error) => mapSubmitError(error),
      },
    );
  };

  const mapSubmitError = (error: unknown) => {
    const { code } = toApiError(error);

    if (code in FILE_ERROR_KEYS) {
      setCvError(t(FILE_ERROR_KEYS[code as keyof typeof FILE_ERROR_KEYS]));
      return;
    }
    if (code === 'application.duplicate') {
      setBannerError(t('public.apply.duplicate'));
      return;
    }
    if (code === 'application.job_not_available' || code === 'http_404') {
      setBannerError(t('public.apply.unavailable'));
      return;
    }
    setBannerError(t('public.apply.error'));
  };

  if (submitted) {
    return (
      <PublicLayout>
        <Card className="py-12">
          <EmptyState
            title={t('public.apply.successTitle')}
            description={t('public.apply.successBody', { title: job.title })}
            action={
              <Link to={`/${slug}`} className="text-sm font-medium text-accent hover:underline">
                {t('public.apply.backToJobs')}
              </Link>
            }
          />
        </Card>
      </PublicLayout>
    );
  }

  return (
    <PublicLayout>
      <div className="mx-auto max-w-xl space-y-6">
        <div className="space-y-1">
          <Link
            to={`/${slug}/jobs/${jobSlug}`}
            className="text-sm text-text-muted transition-colors hover:text-accent"
          >
            {t('public.apply.backToJob')}
          </Link>
          <h1 className="text-2xl font-semibold tracking-tight">
            {t('public.apply.title', { title: job.title })}
          </h1>
        </div>

        {/* Identity confirmed from the candidate account — not re-entered on the form. */}
        <div className="rounded-lg border border-border bg-card px-4 py-3 text-sm">
          <span className="text-text-muted">{t('public.apply.applyingAs')}</span>{' '}
          <span className="font-medium text-text">
            {user.firstName} {user.lastName}
          </span>
          <span className="text-text-muted"> · {user.email}</span>
        </div>

        {bannerError && (
          <div className="rounded-lg border border-danger/40 bg-danger-bg px-4 py-3 text-sm text-danger">
            {bannerError}
          </div>
        )}

        <Card className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label={t('public.apply.phone')}>
              {({ id }) => (
                <Input id={id} value={phone} onChange={(event) => setPhone(event.target.value)} />
              )}
            </Field>
            <Field label={t('public.apply.linkedIn')}>
              {({ id }) => (
                <Input
                  id={id}
                  value={linkedInUrl}
                  onChange={(event) => setLinkedInUrl(event.target.value)}
                  placeholder={t('public.apply.linkedInPlaceholder')}
                />
              )}
            </Field>
          </div>

          <Field label={t('public.apply.coverLetter')}>
            {({ id }) => (
              <Textarea
                id={id}
                rows={5}
                value={coverLetter}
                onChange={(event) => setCoverLetter(event.target.value)}
                placeholder={t('public.apply.coverLetterPlaceholder')}
              />
            )}
          </Field>

          <CvUpload
            file={cv}
            onSelect={selectCv}
            onClear={() => { setCv(null); setCvError(undefined); }}
            error={cvError}
          />

          <div className="pt-2">
            <Button onClick={handleSubmit} disabled={apply.isPending} className="w-full">
              {apply.isPending ? t('public.apply.submitting') : t('public.apply.submit')}
            </Button>
          </div>
        </Card>
      </div>
    </PublicLayout>
  );
}
