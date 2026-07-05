import { NotificationsFeed } from '../components/NotificationsFeed';
import { companyNotifications } from '../useNotifications';

// Routed under AppShell (see App.tsx), which already supplies the sidebar/topbar chrome and the
// content padding — unlike the candidate page, this one renders no layout of its own.
export function CompanyNotificationsPage() {
  return (
    <NotificationsFeed
      hooks={companyNotifications}
      applicationBasePath="/applications"
      subtitleKey="notifications.companySubtitle"
    />
  );
}
