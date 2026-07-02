import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/components/ui';
import { PublicLayout } from './PublicLayout';

/* Shown when a careers slug or job slug doesn't resolve (the backend returns 404). Wraps itself in
   the public layout so callers can render it directly in place of the page body. */
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
