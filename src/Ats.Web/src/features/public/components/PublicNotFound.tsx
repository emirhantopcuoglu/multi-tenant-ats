import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/components/ui';
import { PublicLayout } from './PublicLayout';

/* Includes the layout, so callers can render it in place of the page. */
export function PublicNotFound() {
  const { t } = useTranslation();

  return (
    <PublicLayout>
      <div className="py-16">
        <EmptyState title={t('public.notFound.title')} description={t('public.notFound.description')} />
      </div>
    </PublicLayout>
  );
}
