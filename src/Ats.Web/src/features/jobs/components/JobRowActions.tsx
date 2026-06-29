import { useTranslation } from 'react-i18next';
import { Dropdown, IconButton, type DropdownAction } from '@/components/ui';
import type { Job } from '@/types/job';

export type JobAction = 'edit' | 'publish' | 'close' | 'archive';

interface JobRowActionsProps {
  job: Job;
  onAction: (job: Job, action: JobAction) => void;
}

function KebabIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <circle cx="12" cy="5" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="12" cy="19" r="1.6" />
    </svg>
  );
}

/* The per-row action menu. Items are derived from the job's status so only valid lifecycle
   transitions are offered (publish: Draft only; close: Published only; archive/edit: anything but
   Archived) — matching the domain rules in Job.cs. An archived job exposes no actions, so the menu
   isn't rendered at all. */
export function JobRowActions({ job, onAction }: JobRowActionsProps) {
  const { t } = useTranslation();

  const items: DropdownAction[] = [];
  if (job.status !== 'Archived') {
    items.push({ key: 'edit', label: t('jobs.action.edit'), onSelect: () => onAction(job, 'edit') });
  }
  if (job.status === 'Draft') {
    items.push({ key: 'publish', label: t('jobs.action.publish'), onSelect: () => onAction(job, 'publish') });
  }
  if (job.status === 'Published') {
    items.push({ key: 'close', label: t('jobs.action.close'), onSelect: () => onAction(job, 'close') });
  }
  if (job.status !== 'Archived') {
    items.push({
      key: 'archive',
      label: t('jobs.action.archive'),
      tone: 'danger',
      separatorBefore: true,
      onSelect: () => onAction(job, 'archive'),
    });
  }

  if (items.length === 0) return null;

  return (
    <Dropdown
      align="end"
      items={items}
      trigger={<IconButton aria-label={t('jobs.rowActions')} icon={<KebabIcon />} />}
    />
  );
}
