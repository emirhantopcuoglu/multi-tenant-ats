import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { SidebarNavItem } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { NAV_ITEMS, isNavItemActive } from './navConfig';

interface SidebarProps {
  onNavigate?: () => void;
}

/* SidebarNavItem is a button, so navigation is imperative; no open-in-new-tab. */
export function Sidebar({ onNavigate }: SidebarProps) {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { role } = useAuth();

  const visibleItems = NAV_ITEMS.filter(
    (item) => !item.roles || (role !== null && item.roles.includes(role)),
  );

  const go = (path: string) => {
    navigate(path);
    onNavigate?.();
  };

  return (
    <div className="flex h-full flex-col gap-2 p-4">
      <div className="flex items-center gap-2 px-2 py-2">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent text-sm font-bold text-accent-fg">
          A
        </span>
        <span className="text-base font-semibold tracking-tight">{t('common.appName')}</span>
      </div>

      <nav className="flex flex-col gap-1">
        {visibleItems.map((item) => (
          <SidebarNavItem
            key={item.path}
            icon={item.icon}
            label={t(item.labelKey)}
            active={isNavItemActive(pathname, item.path)}
            onClick={() => go(item.path)}
          />
        ))}
      </nav>
    </div>
  );
}
