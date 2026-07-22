import { candidateNotifications } from '../useNotifications';
import { NotificationBell } from './NotificationBell';

export function CandidateNotificationBell() {
  return (
    <NotificationBell
      hooks={candidateNotifications}
      applicationBasePath="/candidate/applications"
      notificationsPath="/candidate/notifications"
    />
  );
}
