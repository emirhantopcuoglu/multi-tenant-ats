import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui';
import { toApiError } from '@/lib/problemDetails';
import { AuthLayout } from '@/features/auth/components/AuthLayout';
import { useConfirmCandidateEmailChange } from '../useCandidateProfile';

/* Someone registered the address during the one-hour window — worth its own message, because
   "invalid or expired link" would send the user hunting for a fresher mail that won't help. */
const EMAIL_ALREADY_REGISTERED_CODE = 'candidate_profile.email_already_registered';

/* Landing page for the link mailed to the NEW address. Anonymous on purpose — the link may be
   opened on a device with no session; the single-use token in the query string is the proof.

   The confirm is a button click, NOT an automatic effect on mount: corporate mail scanners open
   links (some even execute the page) before the human ever sees the mail, and against a single-use
   token that would spend the change on the scanner's visit. One explicit click keeps the token
   unspent until a person decides. */
export function ConfirmEmailChangePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const confirm = useConfirmCandidateEmailChange();

  const token = searchParams.get('token');

  if (!token) {
    return (
      <AuthLayout title={t('confirmEmailChange.title')} subtitle={t('confirmEmailChange.missingToken')}>
        <span />
      </AuthLayout>
    );
  }

  if (confirm.isSuccess) {
    return (
      <AuthLayout title={t('confirmEmailChange.success')} subtitle={t('confirmEmailChange.successHint')}>
        <Button
          type="button"
          className="w-full"
          onClick={() => navigate('/candidate/login', { replace: true })}
        >
          {t('confirmEmailChange.goToLogin')}
        </Button>
      </AuthLayout>
    );
  }

  const errorMessage = confirm.isError
    ? toApiError(confirm.error).code === EMAIL_ALREADY_REGISTERED_CODE
      ? t('candidateSettings.security.emailTaken')
      : t('confirmEmailChange.failed')
    : null;

  return (
    <AuthLayout title={t('confirmEmailChange.title')} subtitle={t('confirmEmailChange.description')}>
      <div className="space-y-4">
        {errorMessage && (
          <div role="alert" className="rounded-lg bg-danger-bg px-3 py-2 text-sm text-danger">
            {errorMessage}
          </div>
        )}

        <Button
          type="button"
          className="w-full"
          disabled={confirm.isPending}
          onClick={() => confirm.mutate(token)}
        >
          {t('confirmEmailChange.confirm')}
        </Button>
      </div>
    </AuthLayout>
  );
}
