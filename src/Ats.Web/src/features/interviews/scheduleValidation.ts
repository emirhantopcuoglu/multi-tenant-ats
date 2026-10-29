/* Shared by the schedule and reschedule forms. MINIMUM_LEAD_MINUTES mirrors
   Interview.MinimumLeadMinutes; change both together. The backend still checks it. */
export const MINIMUM_LEAD_MINUTES = 15;

/* Null when either input is missing or invalid. */
export function toScheduledAt(date: string, time: string): Date | null {
  if (!date || !time) return null;
  const parsed = new Date(`${date}T${time}`);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function isSlotTooSoon(scheduledAt: Date, now: number = Date.now()): boolean {
  return scheduledAt.getTime() < now + MINIMUM_LEAD_MINUTES * 60_000;
}
