import type { ReactNode } from 'react';

export type IconTimelineTone = 'accent' | 'success' | 'danger' | 'warning' | 'neutral';

/* Application-event vocabulary shared by the candidate tracking page and the company activity
   tab — both render the same kinds of events, so the icon set lives here once. */
export type IconTimelineIcon =
  | 'submitted'
  | 'viewed'
  | 'movedTo'
  | 'hired'
  | 'rejected'
  | 'current'
  | 'upcoming';

/* Soft, tinted circle background + icon colour per tone — the same semantic tones as Badge
   (lib/statusColors). */
const toneClasses: Record<IconTimelineTone, string> = {
  accent: 'bg-accent-subtle text-accent',
  success: 'bg-success-bg text-success',
  danger: 'bg-danger-bg text-danger',
  warning: 'bg-warning-bg text-warning',
  neutral: 'bg-divider text-text-muted',
};

function iconPath(icon: IconTimelineIcon): string {
  switch (icon) {
    case 'submitted':
      return 'M22 2 11 13M22 2l-7 20-4-9-9-4 20-7Z';
    case 'viewed':
      return 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7ZM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z';
    case 'movedTo':
      return 'M5 12h14M13 6l6 6-6 6';
    case 'hired':
      return 'm5 13 4 4L19 7';
    case 'rejected':
      return 'M18 6 6 18M6 6l12 12';
    case 'current':
      return 'M12 8v4l3 3M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
    case 'upcoming':
      return 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
  }
}

function StepIcon({ icon }: { icon: IconTimelineIcon }) {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={iconPath(icon)} />
    </svg>
  );
}

export function IconTimeline({ children }: { children: ReactNode }) {
  return <ol className="relative">{children}</ol>;
}

interface IconTimelineItemProps {
  icon: IconTimelineIcon;
  tone: IconTimelineTone;
  title: string;
  meta?: string;
  children?: ReactNode;
  /** Set on the final item to drop the connecting line below its icon. */
  last?: boolean;
}

/* One event: a tinted icon circle with a connecting line, then the title, timestamp and optional
   body. Neutral-toned items render a muted title so future/inactive steps recede visually. */
export function IconTimelineItem({ icon, tone, title, meta, children, last = false }: IconTimelineItemProps) {
  return (
    <li className="relative flex gap-4 pb-7 last:pb-0">
      {!last && <span aria-hidden="true" className="absolute left-[19px] top-10 bottom-0 w-px bg-border" />}
      <span
        className={`relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${toneClasses[tone]}`}
      >
        <StepIcon icon={icon} />
      </span>
      <div className="space-y-0.5 pt-1.5">
        <p className={tone === 'neutral' ? 'text-sm text-text-muted' : 'text-sm font-medium text-text'}>
          {title}
        </p>
        {meta && <p className="text-xs text-text-muted">{meta}</p>}
        {children}
      </div>
    </li>
  );
}
