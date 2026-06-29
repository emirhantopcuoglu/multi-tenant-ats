import { cn } from '@/lib/cn';

export type AvatarSize = 'sm' | 'md' | 'lg';

const sizeClasses: Record<AvatarSize, string> = {
  sm: 'h-6.5 w-6.5 text-[10.5px]',
  md: 'h-7.5 w-7.5 text-xs',
  lg: 'h-9 w-9 text-sm',
};

/* Derive up to two uppercase initials from a full name ("Elif Yılmaz" → "EY"). */
export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .map((word) => word[0] ?? '')
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

interface AvatarProps {
  name: string;
  size?: AvatarSize;
  className?: string;
}

/* Initials-only avatar (no image source in the API yet). Uses the accent-subtle surface so it reads
   as a person token without competing with primary accent actions. */
export function Avatar({ name, size = 'md', className }: AvatarProps) {
  return (
    <span
      aria-hidden="true"
      title={name}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-full bg-accent-subtle font-semibold text-accent',
        sizeClasses[size],
        className,
      )}
    >
      {initialsOf(name)}
    </span>
  );
}
