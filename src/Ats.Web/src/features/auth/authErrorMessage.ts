import type { TFunction } from 'i18next';
import type { ApiError } from '@/types/api';

/* Only unambiguous codes are mapped; others show the backend's message. `as const` keeps the values
   as literal keys for the type-safe t(). */
const codeToKey = {
  'auth.invalid_credentials': 'authError.invalidCredentials',
  'candidate_auth.invalid_credentials': 'authError.invalidCredentials',
  'invite.invalid_token': 'authError.invitationInvalid',
  'invite.email_in_use': 'authError.emailInUse',
  'auth.email_not_confirmed': 'authError.emailNotConfirmed',
  'auth.invalid_email_confirmation_token': 'authError.confirmationInvalid',
} as const;

/* The login page checks for this code to offer the resend link. */
export const EMAIL_NOT_CONFIRMED_CODE = 'auth.email_not_confirmed';

export function authErrorMessage(error: ApiError, t: TFunction): string {
  const key = codeToKey[error.code as keyof typeof codeToKey];
  if (key) return t(key);
  return error.message || t('authError.generic');
}
