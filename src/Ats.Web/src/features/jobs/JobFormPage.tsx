import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui';

/* Placeholder for the create (/jobs/new) and edit (/jobs/:id/edit) routes so the list's "New job"
   button and row "Edit" action have a real target. The actual job form lands in Step 3.2. */
export function JobFormPage() {
  const { t } = useTranslation();
  return (
    <Card className="space-y-1">
      <h2 className="text-lg font-semibold text-text">{t('jobs.newJob')}</h2>
      <p className="text-sm text-text-muted">{t('jobs.formPlaceholder')}</p>
    </Card>
  );
}
