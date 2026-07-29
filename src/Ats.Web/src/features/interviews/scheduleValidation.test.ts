import { describe, expect, it } from 'vitest';
import { MINIMUM_LEAD_MINUTES, isSlotTooSoon, toScheduledAt } from './scheduleValidation';

/* Both functions take their clock as an argument, which is what makes them testable at all — a
   `Date.now()` read inside would leave these asserting against whenever the suite happens to run. */

describe('toScheduledAt', () => {
  it('combines a date and a time input into one instant', () => {
    const result = toScheduledAt('2026-08-03', '14:30');

    expect(result).not.toBeNull();
    expect(result!.getFullYear()).toBe(2026);
    expect(result!.getMonth()).toBe(7); // zero-based: August
    expect(result!.getDate()).toBe(3);
    expect(result!.getHours()).toBe(14);
    expect(result!.getMinutes()).toBe(30);
  });

  it('reads the inputs as local time, not UTC', () => {
    // `new Date('2026-08-03T14:30')` — no trailing Z — is local by specification. The recruiter
    // picked 14:30 in their own day; parsing it as UTC would shift every interview by the offset.
    const result = toScheduledAt('2026-08-03', '14:30')!;

    expect(result.getHours()).toBe(14);
  });

  it.each([
    ['', '14:30', 'no date'],
    ['2026-08-03', '', 'no time'],
    ['', '', 'neither'],
  ])('returns null with %s / %s (%s)', (date, time) => {
    // A half-filled form is not an error to show yet — the caller distinguishes "not ready" from
    // "wrong", which is why this is null rather than an invalid Date.
    expect(toScheduledAt(date, time)).toBeNull();
  });

  it.each([
    ['not-a-date', '14:30'],
    ['2026-08-03', '99:99'],
    ['2026-13-45', '14:30'],
  ])('returns null for unparseable input %s %s', (date, time) => {
    expect(toScheduledAt(date, time)).toBeNull();
  });
});

describe('isSlotTooSoon', () => {
  const now = new Date('2026-08-03T12:00:00Z').getTime();
  const minutesFromNow = (minutes: number) => new Date(now + minutes * 60_000);

  it('rejects a slot in the past', () => {
    expect(isSlotTooSoon(minutesFromNow(-60), now)).toBe(true);
  });

  it('rejects a slot inside the lead window', () => {
    expect(isSlotTooSoon(minutesFromNow(MINIMUM_LEAD_MINUTES - 1), now)).toBe(true);
  });

  it('accepts a slot exactly at the lead boundary', () => {
    // The comparison is strictly-less-than, so the boundary itself is allowed. An off-by-one here
    // would reject a slot the backend accepts, and the form would refuse a legal booking.
    expect(isSlotTooSoon(minutesFromNow(MINIMUM_LEAD_MINUTES), now)).toBe(false);
  });

  it('accepts a slot comfortably ahead', () => {
    expect(isSlotTooSoon(minutesFromNow(60 * 24 * 14), now)).toBe(false);
  });

  it('mirrors the domain constant', () => {
    // Interview.MinimumLeadMinutes carries the same 15 on the server. The duplication is deliberate
    // and unavoidable, so this asserts the number rather than trusting a comment to keep it honest.
    expect(MINIMUM_LEAD_MINUTES).toBe(15);
  });
});
