import type { ReactNode } from 'react';

export type IconTimelineTone = 'accent' | 'success' | 'danger' | 'warning' | 'neutral';

/* Shared by the candidate tracking page and the company activity tab. */
export type IconTimelineIcon =
  | 'submitted'
  | 'viewed'
  | 'movedTo'
  | 'hired'
  | 'rejected'
  | 'withdrawn'
  | 'current'
  | 'upcoming'
  | 'screening'
  | 'interview'
  | 'offer'
  | 'stage';

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
    case 'withdrawn':
      // Not the rejection cross: the candidate chose to leave.
      return 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9';
    case 'current':
      return 'M12 8v4l3 3M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
    case 'upcoming':
      return 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z';
    case 'screening':
      return 'M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14ZM21 21l-5.2-5.2';
    case 'interview':
      return 'M8 13H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1M10 21H8l-3 2v-2a2 2 0 0 1-2-2M19 8h-8a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6l3 2v-2a2 2 0 0 0 2-2v-5a2 2 0 0 0-2-2Z';
    case 'offer':
      return 'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5ZM14 3v5h5M9 14h6M9 17h4';
    case 'stage':
      return 'M5 21V4M5 4h11l-1.5 3.5L16 11H5';
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
  last?: boolean;
}

/* Neutral items get a muted title so future steps recede. */
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
