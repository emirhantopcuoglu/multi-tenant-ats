/* Client-side mirror of the rules in SubmitApplicationValidator (backend stays authoritative —
   these only exist to give feedback before the round-trip). Keep the two in sync when either
   changes. */

const PHONE_ALLOWED_CHARACTERS = /^\+?[\d\s().-]+$/;
const PHONE_MAX_LENGTH = 40;
// ITU E.164 caps subscriber numbers at 15 digits; 7 is the shortest national number in use.
const PHONE_MIN_DIGITS = 7;
const PHONE_MAX_DIGITS = 15;

export const LINKEDIN_URL_MAX_LENGTH = 300;
export const COVER_LETTER_MAX_LENGTH = 5000;

export function isPlausiblePhone(value: string): boolean {
  if (value.length > PHONE_MAX_LENGTH || !PHONE_ALLOWED_CHARACTERS.test(value)) return false;
  const digitCount = (value.match(/\d/g) ?? []).length;
  return digitCount >= PHONE_MIN_DIGITS && digitCount <= PHONE_MAX_DIGITS;
}

export function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const { protocol } = new URL(value);
    return protocol === 'http:' || protocol === 'https:';
  } catch {
    return false;
  }
}
