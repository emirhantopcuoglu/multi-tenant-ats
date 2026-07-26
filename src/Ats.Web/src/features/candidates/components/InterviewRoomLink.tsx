import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { InterviewStatus } from '@/types/enums';

interface InterviewRoomLinkProps {
  /** Null for a phone screen, which has no room. */
  roomToken: string | null;
  status: InterviewStatus;
}

/* How a candidate reaches (or is told they cannot reach) an interview's room. Extracted because the
   same interview appears on two of their screens — the "My interviews" list and the card on the
   application they applied through — and those two had drifted: one offered the link, the other
   showed nothing at all. One component is what stops them drifting again.

   Three outcomes, and the middle one is easy to miss: a settled interview keeps the label but loses
   the link, so the room's absence reads as "this is over" rather than as a page that forgot to
   render something. */
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
