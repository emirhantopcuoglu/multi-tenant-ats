import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { InterviewStatus } from '@/types/enums';

interface InterviewRoomLinkProps {
  roomToken: string | null;
  status: InterviewStatus;
}

/* Shared by "My interviews" and the application card so they show the same thing. A settled
   interview keeps the label but loses the link. */
export function InterviewRoomLink({ roomToken, status }: InterviewRoomLinkProps) {
  const { t } = useTranslation();

  if (roomToken === null) {
    return (
      <span className="text-sm font-medium text-text-muted">
        {t('candidatePortal.interviews.phone')}
      </span>
    );
  }

  if (status !== 'Scheduled') {
    return (
      <span className="cursor-not-allowed text-sm font-medium text-text-disabled">
        {t('candidatePortal.interviews.openRoom')}
      </span>
    );
  }

  return (
    <Link
      to={`/interview-room/${roomToken}`}
      className="text-sm font-medium text-accent hover:underline"
    >
      {t('candidatePortal.interviews.openRoom')}
    </Link>
  );
}
