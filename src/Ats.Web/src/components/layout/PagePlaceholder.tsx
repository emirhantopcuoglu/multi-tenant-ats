import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui';
import type { NavLabelKey } from './navConfig';

interface PagePlaceholderProps {
  titleKey: NavLabelKey;
}

/* Temporary content for nav destinations whose real screens land in later phases (Jobs, Applications,
   Interviews, Candidates, Overview). Shared so the shell, drawer, and user menu can be exercised
   end to end before those features exist; each route swaps this out for its feature page later. */
export function PagePlaceholder({ titleKey }: PagePlaceholderProps) {
  const { t } = useTranslation();
  return (
    <Card className="space-y-1">
      <h2 className="text-lg font-semibold text-text">{t(titleKey)}</h2>
      <p className="text-sm text-text-muted">{t('placeholder.body')}</p>
    </Card>
  );
}
