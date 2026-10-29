/* Display formatting only; the backend strips formatting and checks the digit count. Pure, so it
   can be tested without a DOM. */

/* Dial countries are separate from residence: where someone lives and their phone's country can
   differ. '#' marks a digit slot. */
export interface PhoneCountry {
  /* ISO 3166-1 alpha-2. */
  iso: string;
  /* Without '+'. */
  dialCode: string;
  template: string;
}

export const PHONE_COUNTRIES: readonly PhoneCountry[] = [
  { iso: 'TR', dialCode: '90', template: '(###) ### ## ##' },
  { iso: 'US', dialCode: '1', template: '(###) ###-####' },
  { iso: 'GB', dialCode: '44', template: '#### ######' },
  { iso: 'DE', dialCode: '49', template: '### ########' },
  { iso: 'FR', dialCode: '33', template: '# ## ## ## ##' },
  { iso: 'NL', dialCode: '31', template: '# ########' },
  { iso: 'ES', dialCode: '34', template: '### ### ###' },
];

export const DEFAULT_PHONE_COUNTRY = PHONE_COUNTRIES[0];

export function digitCapacity(template: string): number {
  return [...template].filter((char) => char === '#').length;
}

/* "5321234567" + "(###) ### ## ##" → "(532) 123 45 67"; "532" → "(532". Literals stop when the
   digits run out, so there's no trailing punctuation. */
export function formatNationalDigits(digits: string, template: string): string {
  let result = '';
  let digitIndex = 0;

  for (const char of template) {
    if (digitIndex >= digits.length) break;
    if (char === '#') {
      result += digits[digitIndex];
      digitIndex += 1;
    } else {
      result += char;
    }
  }

  return result;
}

export function placeholderFor(template: string): string {
  return template.replaceAll('#', 'x');
}

export function composePhoneValue(country: PhoneCountry, nationalDigits: string): string {
  if (nationalDigits.length === 0) return '';
  return `+${country.dialCode} ${formatNationalDigits(nationalDigits, country.template)}`;
}

export interface ParsedPhone {
  country: PhoneCountry;
  nationalDigits: string;
}

/* Splits a stored value back into dial country and digits. Longest dial code wins. Values without a
   known code fall back to the default country, dropping a leading trunk zero. */
export function parsePhoneValue(value: string): ParsedPhone {
  const digits = value.replace(/\D/g, '');
  if (digits.length === 0) return { country: DEFAULT_PHONE_COUNTRY, nationalDigits: '' };

  if (value.trimStart().startsWith('+')) {
    const byLongestDial = [...PHONE_COUNTRIES].sort((a, b) => b.dialCode.length - a.dialCode.length);
    const match = byLongestDial.find((country) => digits.startsWith(country.dialCode));
    if (match) {
      return {
        country: match,
        nationalDigits: digits.slice(match.dialCode.length, match.dialCode.length + digitCapacity(match.template)),
      };
    }
  }

  return {
    country: DEFAULT_PHONE_COUNTRY,
    nationalDigits: digits.replace(/^0/, '').slice(0, digitCapacity(DEFAULT_PHONE_COUNTRY.template)),
  };
}

/* Index just past the Nth digit, so the caret stays put when punctuation changes. */
export function caretIndexAfterDigit(formatted: string, digitCount: number): number {
  if (digitCount <= 0) return 0;

  let seen = 0;
  for (let index = 0; index < formatted.length; index += 1) {
    if (/\d/.test(formatted[index])) {
      seen += 1;
      if (seen === digitCount) return index + 1;
    }
  }

  return formatted.length;
}
