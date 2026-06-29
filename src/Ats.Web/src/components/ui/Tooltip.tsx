import type { ReactNode } from 'react';
import * as RadixTooltip from '@radix-ui/react-tooltip';

interface TooltipProps {
  content: ReactNode;
  children: ReactNode;
  side?: 'top' | 'right' | 'bottom' | 'left';
}

const TOOLTIP_DELAY_MS = 200;

/* Tooltip on Radix: hover + focus triggering, dismissal, and pointer-safe timing handled for us.
   Colours invert (text surface on background) to match the prototype. The Provider is co-located for
   a self-contained component; if many tooltips render at once, a single app-level Provider is the
   optimisation, but it isn't needed yet. */
export function Tooltip({ content, children, side = 'top' }: TooltipProps) {
  return (
    <RadixTooltip.Provider delayDuration={TOOLTIP_DELAY_MS}>
      <RadixTooltip.Root>
        <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
        <RadixTooltip.Portal>
          <RadixTooltip.Content
            side={side}
            sideOffset={6}
            className="z-50 max-w-xs rounded-lg bg-text px-2.5 py-1.5 text-xs text-bg shadow-card"
          >
            {content}
            <RadixTooltip.Arrow className="fill-text" />
          </RadixTooltip.Content>
        </RadixTooltip.Portal>
      </RadixTooltip.Root>
    </RadixTooltip.Provider>
  );
}
