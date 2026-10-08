import { describe, expect, it } from 'vitest';
import { isPlausiblePhone } from './applyValidation';

/* This mirrors SubmitApplicationValidator on the server, which stays authoritative — the client copy
   only exists to answer before the round trip. That makes drift the real risk, so the cases below
   are written as the rule rather than as the regex: character set, digit count, length. If the
   backend rule changes, these should fail. */
describe('isPlausiblePhone', () => {
  it.each([
    '+90 (532) 123 45 67',
    '05321234567',
    '+1 212-555-0147',
    '0532.123.4567',
    '(532) 1234567',
  ])('accepts %s', (value) => {
    // Numbers arrive pasted in every shape, so the separators people actually type must all pass.
    expect(isPlausiblePhone(value)).toBe(true);
  });

  it.each([
    ['123456', 'six digits — below the shortest national number in use'],
    ['1234567890123456', 'sixteen digits — above E.164 subscriber length'],
    ['', 'empty'],
    ['   ', 'whitespace only'],
  ])('rejects %s (%s)', (value) => {
    expect(isPlausiblePhone(value)).toBe(false);
  });

  it.each([
    ['+90 532 123 45 67 ext. 12', 'letters'],
    ['532*1234567', 'an asterisk'],
    ['532/1234567', 'a slash'],
    ['+90;5321234567', 'a semicolon'],
  ])('rejects a number containing %s', (value) => {
    expect(isPlausiblePhone(value)).toBe(false);
  });

  it('accepts the digit-count boundaries', () => {
    // Seven and fifteen are both allowed; an off-by-one either way would reject a real number.
    expect(isPlausiblePhone('1234567')).toBe(true);
    expect(isPlausiblePhone('123456789012345')).toBe(true);
  });

  it('rejects a value longer than the field allows even when its digits are fine', () => {
    // Ten digits padded past 40 characters with legal separators: the digit rule passes and the
    // length rule is what has to catch it.
    const padded = `${' '.repeat(35)}5321234567`;

    expect(padded.length).toBeGreaterThan(40);
    expect(isPlausiblePhone(padded)).toBe(false);
  });

  it('allows a plus only at the front', () => {
    expect(isPlausiblePhone('+905321234567')).toBe(true);
    expect(isPlausiblePhone('905321234567+')).toBe(false);
  });
});
