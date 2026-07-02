import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { SidebarNavItem } from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { NAV_ITEMS, isNavItemActive } from './navConfig';

interface SidebarProps {
  /** Called after a navigation so the mobile drawer can close itself. */
  onNavigate?: () => void;
}

/* The navigation rail: brand mark plus the nav items the current role is allowed to see.
   SidebarNavItem is a button (from Step 1.5), so we navigate imperatively here rather than rendering
   anchors — acceptable for an in-app SPA rail; revisit if open-in-new-tab becomes a requirement. */
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
