/* Pure formatting logic for the masked phone input, kept apart from the component so it can be
   exercised without a DOM. No masking library: the whole need is "fill digits into a template",
   which is a handful of small functions — a dependency would cost more than it saves.

   The composed value stays a plain string like "+90 (532) 123 45 67". The backend already strips
   formatting characters and enforces E.164's 7-15 digits (CandidateAccount.UpdateProfile), so this
   file is presentation only — nothing server-side changes shape. */

/* One entry per supported residence country (types/location.ts). The dial list is deliberately its
   own concept though: where a candidate lives and which country issued their phone number are
   independent facts. '#' marks a digit slot; everything else in a template is literal. */
export interface PhoneCountry {
  /** ISO 3166-1 alpha-2, used as the select value and React key. */
  iso: string;
  /** International dial code without '+'. */
  dialCode: string;
  /** National-number template; '#' is a digit slot. */
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

/* "5321234567" + "(###) ### ## ##" → "(532) 123 45 67"; a partial "532" → "(532".
   Literals are emitted only while digits remain, so the visible value never ends in dangling
   punctuation the user has to delete. O(n) over the template. */
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

/* The placeholder is the template itself with the slots visible: "(xxx) xxx xx xx". */
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

/* Best-effort split of a stored value ("+905321234567" or a previously composed string) back into
   dial country + national digits. Longest dial code wins so "+90..." is Turkey, not a hypothetical
   "+9". Values without a recognisable dial code (legacy free-text entries) fall back to the default
   country, dropping one leading trunk zero ("0532..." is how Turkish numbers are typed locally). */
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

/* Where the caret belongs after reformatting: the index just past the Nth digit of the formatted
   string. Restoring by digit count (not raw index) is what keeps the caret in place when
   formatting inserts or removes punctuation around it. */
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
