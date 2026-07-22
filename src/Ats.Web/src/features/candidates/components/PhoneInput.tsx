import { useLayoutEffect, useRef, useState, type ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Input, Select } from '@/components/ui';
import {
  DEFAULT_PHONE_COUNTRY,
  PHONE_COUNTRIES,
  caretIndexAfterDigit,
  composePhoneValue,
  digitCapacity,
  formatNationalDigits,
  parsePhoneValue,
  placeholderFor,
} from '../phoneFormat';

interface PhoneInputProps {
  id: string;
  /** The composed value as the form stores it, e.g. "+90 (532) 123 45 67" or "". */
  value: string;
  onChange: (value: string) => void;
  describedById?: string;
  invalid?: boolean;
}

/* Masked phone entry: a dial-code select plus a digits-only national-number input that formats as
   the user types. Controlled all the way — the single source of truth is the composed string in
   the form; this component re-derives dial country + digits from it on every render. The one bit
   of internal state is which dial country is selected while the digits are still empty (an empty
   composed value can't carry that information). */
export function PhoneInput({ id, value, onChange, describedById, invalid }: PhoneInputProps) {
  const { t } = useTranslation();
  const parsed = parsePhoneValue(value);
  const [emptyStateIso, setEmptyStateIso] = useState(parsed.country.iso);

  const country =
    parsed.nationalDigits.length > 0
      ? parsed.country
      : (PHONE_COUNTRIES.find((c) => c.iso === emptyStateIso) ?? DEFAULT_PHONE_COUNTRY);

  const formattedNational = formatNationalDigits(parsed.nationalDigits, country.template);

  /* Writing a reformatted value back into a controlled input throws the caret to the end, which
     breaks editing in the middle of the number. Fix: on change, remember how many DIGITS sat left
     of the caret; after React commits the new value, place the caret just past that same digit.
     Counting digits (not characters) keeps the position stable when punctuation shifts around. */
  const inputRef = useRef<HTMLInputElement>(null);
  const pendingCaretDigits = useRef<number | null>(null);

  useLayoutEffect(() => {
    if (pendingCaretDigits.current === null || inputRef.current === null) return;
    const caret = caretIndexAfterDigit(formattedNational, pendingCaretDigits.current);
    inputRef.current.setSelectionRange(caret, caret);
    pendingCaretDigits.current = null;
  }, [formattedNational]);

  function handleNationalChange(event: ChangeEvent<HTMLInputElement>) {
    const raw = event.target.value;
    const caret = event.target.selectionStart ?? raw.length;
    const digitsBeforeCaret = raw.slice(0, caret).replace(/\D/g, '').length;

    /* This strip is the "only digits can go in" rule: letters and symbols vanish before they are
       ever committed, whether typed or pasted. */
    const digits = raw.replace(/\D/g, '').slice(0, digitCapacity(country.template));

    pendingCaretDigits.current = Math.min(digitsBeforeCaret, digits.length);
    onChange(composePhoneValue(country, digits));
  }

  function handleCountryChange(event: ChangeEvent<HTMLSelectElement>) {
    const next = PHONE_COUNTRIES.find((c) => c.iso === event.target.value) ?? DEFAULT_PHONE_COUNTRY;
    setEmptyStateIso(next.iso);

    /* Digits carry over, re-capped and re-formatted under the new country's template. */
    const digits = parsed.nationalDigits.slice(0, digitCapacity(next.template));
    onChange(composePhoneValue(next, digits));
  }

  return (
    <div className="flex gap-2">
      <div className="w-31 shrink-0">
        <Select
          value={country.iso}
          onChange={handleCountryChange}
          aria-label={t('candidateSettings.profile.phoneCountryCode')}
        >
          {PHONE_COUNTRIES.map((c) => (
            <option key={c.iso} value={c.iso}>
              {c.iso} +{c.dialCode}
            </option>
          ))}
        </Select>
      </div>

      <Input
        ref={inputRef}
        id={id}
        type="tel"
        inputMode="tel"
        autoComplete="tel-national"
        placeholder={placeholderFor(country.template)}
        value={formattedNational}
        onChange={handleNationalChange}
        aria-describedby={describedById}
        invalid={invalid}
      />
    </div>
  );
}
