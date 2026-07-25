import { useTranslation } from 'react-i18next';
import { Badge, Skeleton, Table, THead, TBody, TR, TH, TD } from '@/components/ui';
import type { InterviewListItem } from '@/types/interview';
import { InterviewerAvatars } from './InterviewerAvatars';
import { InterviewStatusBadge } from './InterviewStatusBadge';

interface InterviewsTableProps {
  interviews: InterviewListItem[];
  onSelect: (id: string) => void;
}

function ColumnHeaders() {
  const { t } = useTranslation();
  return (
    <TR>
      <TH>{t('interviews.col.candidate')}</TH>
      <TH>{t('interviews.col.type')}</TH>
      <TH>{t('interviews.col.when')}</TH>
      <TH>{t('interviews.col.duration')}</TH>
      <TH>{t('interviews.col.status')}</TH>
      <TH>{t('interviews.col.interviewers')}</TH>
    </TR>
  );
}

export function InterviewsTable({ interviews, onSelect }: InterviewsTableProps) {
  const { t, i18n } = useTranslation();
  const dateTimeFormatter = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {interviews.map((interview) => (
          <TR
            key={interview.id}
            interactive
            onClick={() => onSelect(interview.id)}
            className="cursor-pointer"
          >
            <TD className="font-medium">{interview.candidateName || '—'}</TD>
            <TD>
              <Badge tone="neutral">{t(`interviewType.${interview.type}`)}</Badge>
            </TD>
            <TD className="whitespace-nowrap text-text-muted">
              {dateTimeFormatter.format(new Date(interview.scheduledAtUtc))}
            </TD>
            <TD className="whitespace-nowrap text-text-muted">
              {t('interviews.minutesShort', { count: interview.durationMinutes })}
            </TD>
            <TD>
              <InterviewStatusBadge interview={interview} />
            </TD>
            <TD>
              <InterviewerAvatars interviewerUserIds={interview.interviewerUserIds} />
            </TD>
          </TR>
        ))}
      </TBody>
    </Table>
  );
}

export function InterviewsTableSkeleton() {
  return (
    <Table>
      <THead>
        <ColumnHeaders />
      </THead>
      <TBody>
        {Array.from({ length: 6 }).map((_, rowIndex) => (
          <TR key={rowIndex} interactive>
            {Array.from({ length: 6 }).map((__, cellIndex) => (
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
