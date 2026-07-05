import { companyNotifications } from '../useNotifications';
import { NotificationBell } from './NotificationBell';

export function CompanyNotificationBell() {
  return (
    <NotificationBell
      hooks={companyNotifications}
      applicationBasePath="/applications"
      notificationsPath="/notifications"
    />
  );
}
