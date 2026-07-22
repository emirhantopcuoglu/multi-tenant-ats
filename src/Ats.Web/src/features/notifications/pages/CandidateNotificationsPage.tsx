import { PublicLayout } from '@/features/public/components/PublicLayout';
import { NotificationsFeed } from '../components/NotificationsFeed';
import { candidateNotifications } from '../useNotifications';

export function CandidateNotificationsPage() {
  return (
    <PublicLayout>
      <NotificationsFeed
        hooks={candidateNotifications}
        applicationBasePath="/candidate/applications"
        subtitleKey="notifications.subtitle"
      />
    </PublicLayout>
  );
}
