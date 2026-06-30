import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Field, Input, Skeleton, Textarea } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { PublicLayout } from './components/PublicLayout';
import { PublicNotFound } from './components/PublicNotFound';
import { CvUpload } from './components/CvUpload';
import { usePublicJob } from './usePublicJobs';
import { useApplyToJob } from './useApplyToJob';
import { validateCvFile, type CvFileError } from './cvFile';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Backend file-error codes → i18n keys, shared by the client pre-check and the server's response.
// `as const` keeps the values as literal key types so the type-safe `t()` accepts them.
const FILE_ERROR_KEYS = {
  'file.empty': 'public.apply.fileEmpty',
  'file.too_large': 'public.apply.fileTooLarge',
  'file.unsupported_type': 'public.apply.fileUnsupported',
  'file.content_mismatch': 'public.apply.fileMismatch',
} as const satisfies Record<CvFileError | 'file.content_mismatch', string>;

interface FormErrors {
  firstName?: string;
  lastName?: string;
  email?: string;
  cv?: string;
}

/* Public application form at /{slug}/jobs/{jobSlug}/apply. Validates the required fields and the CV
   client-side, posts multipart, and shows a success state. The backend remains the authority on the
   file (magic bytes), duplicates, and availability, so its error codes map back onto the form. */
export function PublicApplyPage() {
  const { t } = useTranslation();
  const { slug = '', jobSlug = '' } = useParams();
  const jobQuery = usePublicJob(slug, jobSlug);
  const apply = useApplyToJob(slug, jobSlug);

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [linkedInUrl, setLinkedInUrl] = useState('');
  const [coverLetter, setCoverLetter] = useState('');
  const [cv, setCv] = useState<File | null>(null);
  const [errors, setErrors] = useState<FormErrors>({});
  const [bannerError, setBannerError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  if (jobQuery.isLoading) {
    return (
      <PublicLayout>
        <Skeleton className="h-64 w-full" />
      </PublicLayout>
    );
  }

  // The job must exist and be published to apply to it; otherwise the URL is stale or invalid.
  if (jobQuery.isError || !jobQuery.data) {
    return <PublicNotFound />;
  }

  const job = jobQuery.data;

  const selectCv = (file: File) => {
    const code = validateCvFile(file);
    if (code) {
      setCv(null);
      setErrors((current) => ({ ...current, cv: t(FILE_ERROR_KEYS[code]) }));
      return;
    }
    setCv(file);
    setErrors((current) => ({ ...current, cv: undefined }));
  };

  const handleSubmit = () => {
    setBannerError(null);

    const nextErrors: FormErrors = {};
    if (!firstName.trim()) nextErrors.firstName = t('public.apply.required');
    if (!lastName.trim()) nextErrors.lastName = t('public.apply.required');
    if (!email.trim()) nextErrors.email = t('public.apply.required');
    else if (!EMAIL_PATTERN.test(email.trim())) nextErrors.email = t('public.apply.emailInvalid');
    if (!cv) nextErrors.cv = t('public.apply.cvRequired');

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    apply.mutate(
      {
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        phone: phone.trim() || undefined,
        linkedInUrl: linkedInUrl.trim() || undefined,
        coverLetter: coverLetter.trim() || undefined,
        cv: cv!,
      },
      {
        onSuccess: () => setSubmitted(true),
        onError: (error) => mapSubmitError(error),
      },
    );
  };

  // Translate the backend's structured error into a field message or a banner.
  const mapSubmitError = (error: unknown) => {
    const { code } = toApiError(error);

    if (code in FILE_ERROR_KEYS) {
      setErrors((current) => ({ ...current, cv: t(FILE_ERROR_KEYS[code as keyof typeof FILE_ERROR_KEYS]) }));
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

        {bannerError && (
          <div className="rounded-lg border border-danger/40 bg-danger-bg px-4 py-3 text-sm text-danger">
            {bannerError}
          </div>
        )}

        <Card className="space-y-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label={t('public.apply.firstName')} error={errors.firstName}>
              {({ id, describedById, invalid }) => (
                <Input
                  id={id}
                  aria-describedby={describedById}
                  invalid={invalid}
                  value={firstName}
                  onChange={(event) => setFirstName(event.target.value)}
                />
              )}
            </Field>
            <Field label={t('public.apply.lastName')} error={errors.lastName}>
              {({ id, describedById, invalid }) => (
                <Input
                  id={id}
                  aria-describedby={describedById}
                  invalid={invalid}
                  value={lastName}
                  onChange={(event) => setLastName(event.target.value)}
                />
              )}
            </Field>
          </div>

          <Field label={t('public.apply.email')} error={errors.email}>
            {({ id, describedById, invalid }) => (
              <Input
                id={id}
                type="email"
                aria-describedby={describedById}
                invalid={invalid}
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            )}
          </Field>

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
            onClear={() => setCv(null)}
            error={errors.cv}
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
