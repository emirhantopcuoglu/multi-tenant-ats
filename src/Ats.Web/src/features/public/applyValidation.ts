/* Mirrors SubmitApplicationValidator; keep them in sync. The backend is the authority. */

const PHONE_ALLOWED_CHARACTERS = /^\+?[\d\s().-]+$/;
const PHONE_MAX_LENGTH = 40;
// E.164: at most 15 digits.
const PHONE_MIN_DIGITS = 7;
const PHONE_MAX_DIGITS = 15;

export const LINKEDIN_URL_MAX_LENGTH = 300;
export const COVER_LETTER_MAX_LENGTH = 5000;

export function isPlausiblePhone(value: string): boolean {
  if (value.length > PHONE_MAX_LENGTH || !PHONE_ALLOWED_CHARACTERS.test(value)) return false;
  const digitCount = (value.match(/\d/g) ?? []).length;
  return digitCount >= PHONE_MIN_DIGITS && digitCount <= PHONE_MAX_DIGITS;
}
