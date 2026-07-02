import { useTranslation } from 'react-i18next';
import { Badge, Skeleton, Table, THead, TBody, TR, TH, TD } from '@/components/ui';
import { applicationStatusTone } from '@/lib/statusColors';
import type { ApplicationListItem } from '@/types/application';

interface ApplicationsTableProps {
  applications: ApplicationListItem[];
  /** Resolves a job id to its title (the list DTO carries only the id). */
  jobTitleOf: (jobId: string) => string;
  /** Opens the application's detail page. */
  onSelect: (id: string) => void;
}

function ColumnHeaders() {
  const { t } = useTranslation();
  return (
    <TR>
      <TH>{t('applications.col.candidate')}</TH>
      <TH>{t('applications.col.job')}</TH>
      <TH>{t('applications.col.stage')}</TH>
      <TH>{t('applications.col.status')}</TH>
      <TH>{t('applications.col.applied')}</TH>
    </TR>
  );
}

export function ApplicationsTable({ applications, jobTitleOf, onSelect }: ApplicationsTableProps) {
  const { t, i18n } = useTranslation();
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' });

  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {applications.map((application) => (
          <TR
            key={application.id}
            interactive
            onClick={() => onSelect(application.id)}
            className="cursor-pointer"
          >
            <TD>
              <div className="font-medium">{application.candidateName}</div>
              <div className="text-xs text-text-muted">{application.candidateEmail}</div>
            </TD>
            <TD className="text-text-muted">{jobTitleOf(application.jobId)}</TD>
            <TD>
              <Badge tone="neutral">{application.stageName}</Badge>
            </TD>
            <TD>
              <Badge tone={applicationStatusTone[application.status]} dot>
                {t(`status.${application.status}`)}
              </Badge>
            </TD>
            <TD className="whitespace-nowrap text-text-muted">
              {dateFormatter.format(new Date(application.appliedAtUtc))}
            </TD>
          </TR>
        ))}
      </TBody>
    </Table>
  );
}

export function ApplicationsTableSkeleton() {
  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {Array.from({ length: 6 }).map((_, rowIndex) => (
          <TR key={rowIndex} interactive>
            {Array.from({ length: 5 }).map((__, cellIndex) => (
              <TD key={cellIndex}>
                <Skeleton className="h-4 w-full max-w-32" />
              </TD>
            ))}
          </TR>
        ))}
      </TBody>
    </Table>
  );
}
