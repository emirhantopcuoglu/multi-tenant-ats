/* Presets keep the URL short (?range=next7); bounds are computed relative to now. */
export const DATE_RANGES = ['all', 'upcoming', 'next7', 'past'] as const;
export type DateRange = (typeof DATE_RANGES)[number];

export function isDateRange(value: string | null): value is DateRange {
  return DATE_RANGES.includes(value as DateRange);
}

const DAYS_IN_WEEK = 7;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

export interface DateBounds {
  fromDate?: string;
  toDate?: string;
}

export function resolveDateRange(range: DateRange, now: Date = new Date()): DateBounds {
  const nowIso = now.toISOString();
  switch (range) {
    case 'upcoming':
      return { fromDate: nowIso };
    case 'next7':
      return { fromDate: nowIso, toDate: new Date(now.getTime() + DAYS_IN_WEEK * MS_PER_DAY).toISOString() };
    case 'past':
      return { toDate: nowIso };
    case 'all':
    default:
      return {};
  }
}
