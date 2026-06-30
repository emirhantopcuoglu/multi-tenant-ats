import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';

/* Company tab. The tenant's name and URL slug are read-only here: they are set at registration and
   there is no update endpoint yet, so showing them as editable fields would promise something the
   API can't keep. Values come from /auth/me via the auth context (no extra request). */
export function CompanyTab() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenant = user?.tenant;

  return (
    <Card className="max-w-xl space-y-4">
      <ReadonlyRow label={t('settings.company.name')} value={tenant?.companyName} />
      <ReadonlyRow label={t('settings.company.slug')} value={tenant?.slug} mono />
      <p className="text-xs text-text-muted">{t('settings.company.readonlyHint')}</p>
    </Card>
  );
}

function ReadonlyRow({ label, value, mono }: { label: string; value?: string; mono?: boolean }): ReactNode {
  return (
    <div className="space-y-1">
      <span className="block text-sm font-medium text-text">{label}</span>
      <p className={mono ? 'font-mono text-sm text-text-muted' : 'text-sm text-text-muted'}>{value ?? '—'}</p>
    </div>
  );
}
