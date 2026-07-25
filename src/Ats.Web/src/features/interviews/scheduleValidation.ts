/* Shared slot validation for the schedule and reschedule forms, which previously each carried their
   own copy of "must be in the future".

   MINIMUM_LEAD_MINUTES mirrors Interview.MinimumLeadMinutes in the domain. The duplication is
   deliberate and unavoidable — the value has to exist in both languages to give immediate feedback
   without a round trip — but the backend stays the authority: it re-checks the same rule and answers
   400 regardless of what the form let through. If the domain constant changes, change it here too. */
export const MINIMUM_LEAD_MINUTES = 15;

/** Builds the instant from a date input and a time input, or null when either is missing/invalid. */
export function toScheduledAt(date: string, time: string): Date | null {
  if (!date || !time) return null;
  const parsed = new Date(`${date}T${time}`);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

/** True when the slot is in the past or too close to now for the invitation to be useful. */
export function isSlotTooSoon(scheduledAt: Date, now: number = Date.now()): boolean {
  return scheduledAt.getTime() < now + MINIMUM_LEAD_MINUTES * 60_000;
}
