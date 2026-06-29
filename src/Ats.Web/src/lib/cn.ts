type ClassValue = string | false | null | undefined;

/* Minimal className joiner: drops falsy values and joins the rest with a space, so components can
   compose conditional classes (`cn('base', isActive && 'active', className)`) cleanly.
   Deliberately not pulling in clsx + tailwind-merge: our components own their class lists and rarely
   need to dedupe conflicting Tailwind utilities. If consumer overrides start conflicting, revisit. */
export function cn(...classes: ClassValue[]): string {
  return classes.filter(Boolean).join(' ');
}

/* Shared focus-visible ring, matching the prototype's `outline:2px solid var(--accent)`.
   Centralized so every interactive primitive stays keyboard-accessible the same way. */
export const focusRing =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-bg';
