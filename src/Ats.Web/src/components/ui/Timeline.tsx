import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

export type TimelineDotTone = 'accent' | 'success' | 'danger' | 'warning' | 'neutral';

const dotColor: Record<TimelineDotTone, string> = {
  accent: 'bg-accent',
  success: 'bg-success',
  danger: 'bg-danger',
  warning: 'bg-warning',
  neutral: 'bg-text-muted',
};

export function Timeline({ children }: { children: ReactNode }) {
  return <ol className="relative">{children}</ol>;
}

interface TimelineItemProps {
  title: ReactNode;
  meta?: ReactNode;
  tone?: TimelineDotTone;
  children?: ReactNode;
  /** Set on the final item to drop the connecting line below its dot. */
  last?: boolean;
}

/* One activity entry: a coloured dot with a connecting line, then the title/body/meta. Used by the
   application activity timeline (Step 3.5). The dot's ring uses the card colour so it reads as a
   node sitting on the line. */
export function TimelineItem({ title, meta, tone = 'neutral', children, last = false }: TimelineItemProps) {
  return (
    <li className="relative flex gap-3 pb-5 last:pb-0">
      {!last && <span aria-hidden="true" className="absolute left-[5px] top-3.5 bottom-0 w-px bg-border" />}
      <span
        aria-hidden="true"
        className={cn(
          'relative z-10 mt-1 h-2.5 w-2.5 shrink-0 rounded-full ring-4 ring-card',
          dotColor[tone],
        )}
      />
      <div className="space-y-0.5">
        <p className="text-sm text-text">{title}</p>
        {children}
        {meta && <p className="text-xs text-text-muted">{meta}</p>}
      </div>
    </li>
  );
}
