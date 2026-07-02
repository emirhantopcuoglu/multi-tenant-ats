import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tabs, TabPanel, type TabItem } from '@/components/ui';
import { CompanyTab } from './components/CompanyTab';
import { UsersTab } from './components/UsersTab';

const TABS = { company: 'company', users: 'users' } as const;
type SettingsTab = (typeof TABS)[keyof typeof TABS];

/* Settings (Step 4.2), Admin-only via the route guard. Two tabs: a read-only Company panel and the
   tenant user directory with invite. The page owns only the active-tab state; each tab fetches its
   own data so switching tabs doesn't re-render the other. */
export function SettingsPage() {
  const { t } = useTranslation();
  const [tab, setTab] = useState<SettingsTab>(TABS.company);

  const items: TabItem[] = [
    { value: TABS.company, label: t('settings.tabs.company') },
    { value: TABS.users, label: t('settings.tabs.users') },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-text">{t('settings.title')}</h1>
        <p className="text-sm text-text-muted">{t('settings.subtitle')}</p>
      </div>

      <Tabs value={tab} onValueChange={(value) => setTab(value as SettingsTab)} items={items}>
        <TabPanel value={TABS.company}>
          <CompanyTab />
        </TabPanel>
        <TabPanel value={TABS.users}>
          <UsersTab />
        </TabPanel>
      </Tabs>
    </div>
  );
}
