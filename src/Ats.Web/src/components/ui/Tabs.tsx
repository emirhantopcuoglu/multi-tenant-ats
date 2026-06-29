import type { ReactNode } from 'react';
import * as RadixTabs from '@radix-ui/react-tabs';

export interface TabItem {
  value: string;
  label: ReactNode;
}

interface TabsProps {
  value: string;
  onValueChange: (value: string) => void;
  items: TabItem[];
  /** TabPanel elements for each tab value. */
  children: ReactNode;
}

/* Tabs on Radix: roving focus, arrow-key navigation, and the aria tab/tabpanel wiring are handled
   for us. Triggers show the active state via Radix's `data-[state=active]`. */
export function Tabs({ value, onValueChange, items, children }: TabsProps) {
  return (
    <RadixTabs.Root value={value} onValueChange={onValueChange}>
      <RadixTabs.List className="flex gap-1 border-b border-border">
        {items.map((tab) => (
          <RadixTabs.Trigger
            key={tab.value}
            value={tab.value}
            className="-mb-px border-b-2 border-transparent px-3 py-2 text-sm font-medium text-text-muted outline-none transition-colors hover:text-text focus-visible:text-text data-[state=active]:border-accent data-[state=active]:text-accent"
          >
            {tab.label}
          </RadixTabs.Trigger>
        ))}
      </RadixTabs.List>
      {children}
    </RadixTabs.Root>
  );
}

export function TabPanel({ value, children }: { value: string; children: ReactNode }) {
  return (
    <RadixTabs.Content value={value} className="pt-4 focus:outline-none">
      {children}
    </RadixTabs.Content>
  );
}
