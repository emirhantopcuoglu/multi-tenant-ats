type ClassValue = string | false | null | undefined;

/* No clsx/tailwind-merge: components own their classes and rarely need conflict merging. */
export function cn(...classes: ClassValue[]): string {
  return classes.filter(Boolean).join(' ');
}

/* Shared so every interactive primitive gets the same focus ring. */
export const focusRing =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-bg';
