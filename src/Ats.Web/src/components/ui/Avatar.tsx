import { cn } from '@/lib/cn';

export type AvatarSize = 'sm' | 'md' | 'lg';

const sizeClasses: Record<AvatarSize, string> = {
  sm: 'h-6.5 w-6.5 text-[10.5px]',
  md: 'h-7.5 w-7.5 text-xs',
  lg: 'h-9 w-9 text-sm',
};

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

/* Initials only; the API has no images. */
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
