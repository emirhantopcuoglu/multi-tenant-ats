import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui';
import { interviewDisplayStatus, interviewDisplayStatusTone } from '@/lib/statusColors';
import type { InterviewStatus } from '@/types/enums';

interface InterviewStatusBadgeProps {
  interview: { status: InterviewStatus; isAwaitingOutcome: boolean };
}

/* Shared by three screens so an elapsed, unresolved interview never reads as "Scheduled". */
export function InterviewStatusBadge({ interview }: InterviewStatusBadgeProps) {
  const { t } = useTranslation();
  const status = interviewDisplayStatus(interview);

  return (
    <Badge tone={interviewDisplayStatusTone[status]} dot>
      {t(`interviewStatus.${status}`)}
    </Badge>
  );
}
