import { useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Avatar, Badge, Dropdown, IconButton } from '@/components/ui';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { useAuth } from '@/app/auth/auth-context';
import { NAV_ITEMS, isNavItemActive } from './navConfig';

interface TopbarProps {
  /** Opens the mobile navigation drawer (the rail is hidden below the lg breakpoint). */
  onMenuClick: () => void;
}

function MenuIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M3 6h18M3 12h18M3 18h18" />
    </svg>
  );
}

function ChevronDownIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className="text-text-muted">
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

function LogoutIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="m16 17 5-5-5-5M21 12H9" />
    </svg>
  );
}

/* The page chrome above the routed content: a mobile menu button, the current page title (derived
   from the active nav item), the theme/language controls, and the user menu that surfaces the
   /auth/me identity plus sign-out. */
export function Topbar({ onMenuClick }: TopbarProps) {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const { user, logout } = useAuth();

  const activeItem = NAV_ITEMS.find((item) => isNavItemActive(pathname, item.path));
  const title = activeItem ? t(activeItem.labelKey) : t('common.appName');
  const fullName = user ? `${user.firstName} ${user.lastName}` : '';
  const companyUser = user?.kind === 'company' ? user : null;

  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border bg-bg/80 px-4 backdrop-blur sm:px-6 lg:px-8">
      <IconButton
        aria-label={t('common.openMenu')}
        icon={<MenuIcon />}
        onClick={onMenuClick}
        className="lg:hidden"
      />
      <h1 className="text-base font-semibold tracking-tight">{title}</h1>

      <div className="ml-auto flex items-center gap-2 sm:gap-3">
        <LanguageSwitcher />
        <ThemeToggle />

        {user && (
          <Dropdown
            align="end"
            header={
              <div className="space-y-1.5">
                <p className="text-sm font-medium text-text">{fullName}</p>
                <p className="text-xs text-text-muted">{user.email}</p>
                {companyUser && (
                  <div className="flex items-center gap-2 pt-0.5">
                    <Badge tone="accent">{t(`role.${companyUser.role}`)}</Badge>
                    <span className="truncate text-xs text-text-muted">{companyUser.tenant.companyName}</span>
                  </div>
                )}
              </div>
            }
            items={[
              {
                key: 'logout',
                label: t('common.signOut'),
                tone: 'danger',
                icon: <LogoutIcon />,
                onSelect: () => {
                  void logout();
                },
              },
            ]}
            trigger={
              <button
                type="button"
                className="flex items-center gap-2 rounded-lg border border-transparent px-1.5 py-1 transition-colors hover:bg-divider focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                <Avatar name={fullName} size="md" />
                <span className="hidden text-left sm:block">
                  <span className="block text-sm font-medium leading-tight text-text">{fullName}</span>
                  {companyUser && (
                    <span className="block text-xs leading-tight text-text-muted">
                      {t(`role.${companyUser.role}`)}
                    </span>
                  )}
                </span>
                <ChevronDownIcon />
              </button>
            }
          />
        )}
      </div>
    </header>
  );
}
