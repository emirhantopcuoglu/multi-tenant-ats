import { useTranslation } from 'react-i18next';
import { Badge, Skeleton, Table, THead, TBody, TR, TH, TD } from '@/components/ui';
import { jobStatusTone } from '@/lib/statusColors';
import type { Job } from '@/types/job';
import { JobRowActions, type JobAction } from './JobRowActions';

interface JobsTableProps {
  jobs: Job[];
  /** Whether the current role may run lifecycle actions (adds the actions column). */
  canManage: boolean;
  onAction: (job: Job, action: JobAction) => void;
}

const SKELETON_ROW_COUNT = 6;

function ColumnHeaders({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation();
  return (
    <TR>
      <TH>{t('jobs.col.title')}</TH>
      <TH>{t('jobs.col.department')}</TH>
      <TH>{t('jobs.col.location')}</TH>
      <TH>{t('jobs.col.type')}</TH>
      <TH>{t('jobs.col.level')}</TH>
      <TH>{t('jobs.col.status')}</TH>
      <TH>{t('jobs.col.created')}</TH>
      {canManage && <TH className="sr-only">{t('jobs.rowActions')}</TH>}
    </TR>
  );
}

export function JobsTable({ jobs, canManage, onAction }: JobsTableProps) {
  const { t, i18n } = useTranslation();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });

  return (
    <Table>
      <THead>
        <ColumnHeaders canManage={canManage} />
      </THead>
      <TBody>
        {jobs.map((job) => (
          <TR key={job.id} interactive>
            <TD className="font-medium">{job.title}</TD>
            <TD className="text-text-muted">{job.department || '—'}</TD>
            <TD className="text-text-muted">{job.location || '—'}</TD>
            <TD className="text-text-muted">{t(`employmentType.${job.employmentType}`)}</TD>
            <TD className="text-text-muted">{t(`experienceLevel.${job.experienceLevel}`)}</TD>
            <TD>
              <Badge tone={jobStatusTone[job.status]} dot>
                {t(`status.${job.status}`)}
              </Badge>
            </TD>
            <TD className="whitespace-nowrap text-text-muted">
              {dateFormatter.format(new Date(job.createdAtUtc))}
            </TD>
            {canManage && (
              <TD className="text-right">
                <JobRowActions job={job} onAction={onAction} />
              </TD>
            )}
          </TR>
        ))}
      </TBody>
    </Table>
  );
}

/* Loading skeleton with the same column count, so the table doesn't reflow when real rows arrive. */
export function JobsTableSkeleton({ canManage }: { canManage: boolean }) {
  const columnCount = canManage ? 8 : 7;

  return (
    <Table>
      <THead>
        <ColumnHeaders canManage={canManage} />
      </THead>
      <TBody>
        {Array.from({ length: SKELETON_ROW_COUNT }).map((_, rowIndex) => (
          <TR key={rowIndex} interactive>
            {Array.from({ length: columnCount }).map((__, cellIndex) => (
              <TD key={cellIndex}>
                <Skeleton className="h-4 w-full max-w-28" />
              </TD>
            ))}
          </TR>
        ))}
      </TBody>
    </Table>
  );
}
