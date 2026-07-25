import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui';
import { interviewDisplayStatus, interviewDisplayStatusTone } from '@/lib/statusColors';
import type { InterviewStatus } from '@/types/enums';

interface InterviewStatusBadgeProps {
  interview: { status: InterviewStatus; isAwaitingOutcome: boolean };
}

/* The one place an interview's status turns into a label. Extracted because three screens render it
   — the list, the application's interviews tab and the detail header — and the whole point of this
   badge is that an elapsed-but-unresolved interview must not read as "Scheduled" on any of them. */
export function InterviewStatusBadge({ interview }: InterviewStatusBadgeProps) {
  const { t } = useTranslation();
  const status = interviewDisplayStatus(interview);

  return (
    <Badge tone={interviewDisplayStatusTone[status]} dot>
      {t(`interviewStatus.${status}`)}
    </Badge>
  );
}
