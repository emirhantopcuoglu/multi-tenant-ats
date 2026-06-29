import type { TFunction } from 'i18next';
import type { ApiError } from '@/types/api';

/* Map backend error codes to friendly, localized messages. Only codes with a single clear meaning
   are mapped; ambiguous ones (e.g. auth.registration_failed covers both "slug taken" and "email
   registered") fall back to the backend's own message, which is already specific and user-safe.
   `as const` keeps the values as literal translation keys so the type-safe t() accepts them. */
const codeToKey = {
  'auth.invalid_credentials': 'authError.invalidCredentials',
  'invite.invalid_token': 'authError.invitationInvalid',
  'invite.email_in_use': 'authError.emailInUse',
} as const;

export function authErrorMessage(error: ApiError, t: TFunction): string {
  const key = codeToKey[error.code as keyof typeof codeToKey];
  if (key) return t(key);
  return error.message || t('authError.generic');
}
