/* Barrel for the shared UI primitives, so screens import from '@/components/ui' rather than
   reaching into individual files. */
export { Button, type ButtonVariant } from './Button';
export { IconButton } from './IconButton';
export { Input } from './Input';
export { Textarea } from './Textarea';
export { Select } from './Select';
export { Checkbox } from './Checkbox';
export { Toggle } from './Toggle';
export { Badge } from './Badge';
export { Avatar, initialsOf } from './Avatar';
export { Card } from './Card';
export { StatCard } from './StatCard';
export { Skeleton } from './Skeleton';
export { EmptyState } from './EmptyState';
export { Field } from './Field';

// Interactive / overlay components (Radix-based where accessibility is hard to hand-roll).
export { Modal } from './Modal';
export { Dropdown, type DropdownAction } from './Dropdown';
export { Tabs, TabPanel, type TabItem } from './Tabs';
export { Tooltip } from './Tooltip';
export { ToastProvider } from './toast/ToastProvider';
export { useToast, type ToastOptions, type ToastTone } from './toast/toast-context';
export {
  Table,
  THead,
  TBody,
  TR,
  TH,
  SortableTH,
  TD,
  TableFooter,
  type SortDirection,
} from './Table';
export { Pagination } from './Pagination';
export { Breadcrumb, type BreadcrumbItem } from './Breadcrumb';
export { Timeline, TimelineItem, type TimelineDotTone } from './Timeline';
export { KanbanColumn, KanbanCard } from './Kanban';
export { SidebarNavItem } from './SidebarNavItem';
