import { NotificationsFeed } from '../components/NotificationsFeed';
import { companyNotifications } from '../useNotifications';

// AppShell supplies the layout, so this page has none.
export function CompanyNotificationsPage() {
  return (
    <NotificationsFeed
      hooks={companyNotifications}
      applicationBasePath="/applications"
      subtitleKey="notifications.companySubtitle"
    />
  );
}
