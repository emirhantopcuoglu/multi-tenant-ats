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
  value: string;
  onChange: (value: string) => void;
  describedById?: string;
  invalid?: boolean;
}

/* Controlled: the composed string in the form is the source of truth. The only local state is the
   dial country chosen while the digits are still empty. */
export function PhoneInput({ id, value, onChange, describedById, invalid }: PhoneInputProps) {
  const { t } = useTranslation();
  const parsed = parsePhoneValue(value);
  const [emptyStateIso, setEmptyStateIso] = useState(parsed.country.iso);

  const country =
    parsed.nationalDigits.length > 0
      ? parsed.country
      : (PHONE_COUNTRIES.find((c) => c.iso === emptyStateIso) ?? DEFAULT_PHONE_COUNTRY);

  const formattedNational = formatNationalDigits(parsed.nationalDigits, country.template);

  /* Reformatting moves the caret to the end. Remember how many digits were left of it and put it back
     after that digit; counting digits survives punctuation changes. */
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

    /* Only digits get in, typed or pasted. */
    const digits = raw.replace(/\D/g, '').slice(0, digitCapacity(country.template));

    pendingCaretDigits.current = Math.min(digitsBeforeCaret, digits.length);
    onChange(composePhoneValue(country, digits));
  }

  function handleCountryChange(event: ChangeEvent<HTMLSelectElement>) {
    const next = PHONE_COUNTRIES.find((c) => c.iso === event.target.value) ?? DEFAULT_PHONE_COUNTRY;
    setEmptyStateIso(next.iso);

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
