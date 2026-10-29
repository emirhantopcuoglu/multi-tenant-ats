import type { ReactNode } from 'react';
import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { cn } from '@/lib/cn';

export interface DropdownAction {
  key: string;
  label: ReactNode;
  onSelect: () => void;
  icon?: ReactNode;
  tone?: 'default' | 'danger';
  separatorBefore?: boolean;
  disabled?: boolean;
}

interface DropdownProps {
  /* Rendered via asChild. */
  trigger: ReactNode;
  items: DropdownAction[];
  align?: 'start' | 'end';
  /* Non-interactive section above the items. */
  header?: ReactNode;
}

const itemToneClasses: Record<NonNullable<DropdownAction['tone']>, string> = {
  default: 'text-text',
  danger: 'text-danger',
};

export function Dropdown({ trigger, items, align = 'end', header }: DropdownProps) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>{trigger}</DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align={align}
          sideOffset={6}
          className="z-50 min-w-[11rem] rounded-xl border border-border bg-elevated p-1.5 shadow-card"
        >
          {header && (
            <>
              <div className="px-2.5 py-2">{header}</div>
              <DropdownMenu.Separator className="my-1.5 h-px bg-divider" />
            </>
          )}
          {items.map((item) => (
            <div key={item.key}>
              {item.separatorBefore && (
                <DropdownMenu.Separator className="my-1.5 h-px bg-divider" />
              )}
              <DropdownMenu.Item
                disabled={item.disabled}
                onSelect={item.onSelect}
                className={cn(
                  'flex cursor-pointer items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm outline-none transition-colors data-[highlighted]:bg-divider data-[disabled]:cursor-not-allowed data-[disabled]:opacity-50',
                  itemToneClasses[item.tone ?? 'default'],
                )}
              >
                {item.icon}
                {item.label}
              </DropdownMenu.Item>
            </div>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
