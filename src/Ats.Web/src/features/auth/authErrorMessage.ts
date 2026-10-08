import type { TFunction } from 'i18next';
import type { ApiError } from '@/types/api';

/* Map backend error codes to friendly, localized messages. Only codes with a single clear meaning
   are mapped; ambiguous ones (e.g. auth.registration_failed covers both "slug taken" and "email
   registered") fall back to the backend's own message, which is already specific and user-safe.
   `as const` keeps the values as literal translation keys so the type-safe t() accepts them. */
const codeToKey = {
  'auth.invalid_credentials': 'authError.invalidCredentials',
  'candidate_auth.invalid_credentials': 'authError.invalidCredentials',
  'invite.invalid_token': 'authError.invitationInvalid',
  'invite.email_in_use': 'authError.emailInUse',
  'auth.email_not_confirmed': 'authError.emailNotConfirmed',
  'auth.invalid_email_confirmation_token': 'authError.confirmationInvalid',
} as const;

/* The login form needs to recognise this one specifically, not just render its message: it is the only
   failure the user can act on from that screen, via the resend link. Exported as a constant so the
   page and the map above cannot drift apart. */
export const EMAIL_NOT_CONFIRMED_CODE = 'auth.email_not_confirmed';

export function authErrorMessage(error: ApiError, t: TFunction): string {
  const key = codeToKey[error.code as keyof typeof codeToKey];
  if (key) return t(key);
  return error.message || t('authError.generic');
}
