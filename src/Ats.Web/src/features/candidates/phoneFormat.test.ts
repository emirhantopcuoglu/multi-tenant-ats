import { describe, expect, it } from 'vitest';
import {
  DEFAULT_PHONE_COUNTRY,
  PHONE_COUNTRIES,
  caretIndexAfterDigit,
  composePhoneValue,
  digitCapacity,
  formatNationalDigits,
  parsePhoneValue,
  placeholderFor,
} from './phoneFormat';

/* The first tests in this codebase's frontend, and phoneFormat is the reason it is a good place to
   start: every function here is pure, none of it touches a DOM, and all of it is the kind of
   index-arithmetic that reads as correct and is not.

   What is deliberately not covered: that the input component wires these up. That needs a DOM and a
   different kind of test; these pin the logic underneath it. */

const turkey = PHONE_COUNTRIES.find((c) => c.iso === 'TR')!;
const unitedStates = PHONE_COUNTRIES.find((c) => c.iso === 'US')!;

describe('formatNationalDigits', () => {
  it('fills a complete number into its template', () => {
    expect(formatNationalDigits('5321234567', turkey.template)).toBe('(532) 123 45 67');
  });

  it('stops at the last digit typed instead of leaving dangling punctuation', () => {
    // The property the loop's early break exists for: a user who has typed three digits sees
    // "(532", not "(532) " with a separator they then have to delete.
    expect(formatNationalDigits('532', turkey.template)).toBe('(532');
    expect(formatNationalDigits('5321', turkey.template)).toBe('(532) 1');
  });

  it('returns nothing for no digits', () => {
    expect(formatNationalDigits('', turkey.template)).toBe('');
  });

  it('ignores digits beyond the template capacity', () => {
    // A paste of a longer number must not grow the mask.
    expect(formatNationalDigits('53212345678888', turkey.template)).toBe('(532) 123 45 67');
  });
});

describe('digitCapacity and placeholderFor', () => {
  it('counts the slots in a template', () => {
    expect(digitCapacity(turkey.template)).toBe(10);
    expect(digitCapacity(unitedStates.template)).toBe(10);
  });

  it('shows the template with its slots visible', () => {
    expect(placeholderFor(turkey.template)).toBe('(xxx) xxx xx xx');
  });

  it.each(PHONE_COUNTRIES)('$iso has a template with at least seven slots', (country) => {
    // E.164's shortest national number is 7 digits, which the server enforces. A template that
    // cannot hold one would make a valid number untypeable in the UI.
    expect(digitCapacity(country.template)).toBeGreaterThanOrEqual(7);
  });
});

describe('composePhoneValue', () => {
  it('prefixes the dial code', () => {
    expect(composePhoneValue(turkey, '5321234567')).toBe('+90 (532) 123 45 67');
  });

  it('produces an empty string rather than a bare dial code when nothing is typed', () => {
    // "+90 " in a cleared field would be submitted as a phone number that is only a country.
    expect(composePhoneValue(turkey, '')).toBe('');
  });
});

describe('parsePhoneValue', () => {
  it('round-trips a composed value', () => {
    const composed = composePhoneValue(unitedStates, '2125550147');

    const parsed = parsePhoneValue(composed);

    expect(parsed.country.iso).toBe('US');
    expect(parsed.nationalDigits).toBe('2125550147');
  });

  it('reads a stored E.164 string with no formatting', () => {
    const parsed = parsePhoneValue('+905321234567');

    expect(parsed.country.iso).toBe('TR');
    expect(parsed.nationalDigits).toBe('5321234567');
  });

  it('drops one leading trunk zero from a local-format legacy value', () => {
    // Turkish numbers are typed locally as "0532...". Without the strip the leading zero would eat
    // a template slot and shift every later digit.
    const parsed = parsePhoneValue('0532 123 45 67');

    expect(parsed.country.iso).toBe(DEFAULT_PHONE_COUNTRY.iso);
    expect(parsed.nationalDigits).toBe('5321234567');
  });

  it('falls back to the default country for an unrecognised dial code', () => {
    const parsed = parsePhoneValue('+999123456789');

    expect(parsed.country.iso).toBe(DEFAULT_PHONE_COUNTRY.iso);
  });

  it('returns empty for a value with no digits at all', () => {
    expect(parsePhoneValue('').nationalDigits).toBe('');
    expect(parsePhoneValue('not a phone').nationalDigits).toBe('');
  });

  it('prefers the longest matching dial code', () => {
    // The sort in parsePhoneValue exists for this: "1" is a prefix of nothing here, but the moment
    // a two-digit code starting with an existing one-digit code is added, a naive first-match
    // would route it to the wrong country. Asserting the rule now means that addition cannot
    // quietly break parsing.
    const byLongestFirst = [...PHONE_COUNTRIES].sort(
      (a, b) => b.dialCode.length - a.dialCode.length,
    );

    for (const country of byLongestFirst) {
      const digits = '1'.repeat(digitCapacity(country.template));

      expect(parsePhoneValue(`+${country.dialCode}${digits}`).country.iso).toBe(country.iso);
    }
  });
});

describe('caretIndexAfterDigit', () => {
  it('returns the index just past the nth digit', () => {
    // "(532) 123 45 67" — the 3rd digit is at index 3, so the caret belongs at 4, before the ")".
    expect(caretIndexAfterDigit('(532) 123 45 67', 3)).toBe(4);
  });

  it('skips over separators when counting', () => {
    // The 4th digit sits after ") ", which a raw index would have landed inside.
    expect(caretIndexAfterDigit('(532) 123 45 67', 4)).toBe(7);
  });

  it('puts the caret at the start for no digits', () => {
    expect(caretIndexAfterDigit('(532) 123 45 67', 0)).toBe(0);
    expect(caretIndexAfterDigit('(532) 123 45 67', -1)).toBe(0);
  });

  it('clamps to the end when asked for more digits than exist', () => {
    const formatted = '(532) 123 45 67';

    expect(caretIndexAfterDigit(formatted, 99)).toBe(formatted.length);
  });
});
