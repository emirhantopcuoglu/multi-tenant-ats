import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

/* The persistent application shell wrapping every authenticated route. On lg+ the sidebar is fixed
   and the content is inset to clear it; below lg the sidebar collapses into an off-canvas drawer
   toggled from the Topbar. The routed screen renders through <Outlet/>. */
export function AppShell() {
  const { t } = useTranslation();
  const [isDrawerOpen, setDrawerOpen] = useState(false);
  const closeDrawer = () => setDrawerOpen(false);

  // Dismiss the drawer on Escape, matching the overlay-dismissal convention of our other overlays.
  useEffect(() => {
    if (!isDrawerOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setDrawerOpen(false);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [isDrawerOpen]);

  return (
    <div className="min-h-screen bg-bg text-text">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 border-r border-border bg-card lg:flex lg:flex-col">
        <Sidebar />
      </aside>

      {isDrawerOpen && (
        <div className="lg:hidden">
          <button
            type="button"
            aria-label={t('common.closeMenu')}
            onClick={closeDrawer}
            className="fixed inset-0 z-40 bg-black/40"
          />
          <aside className="fixed inset-y-0 left-0 z-50 flex w-64 flex-col border-r border-border bg-card">
            <Sidebar onNavigate={closeDrawer} />
          </aside>
        </div>
      )}

      <div className="flex min-h-screen flex-col lg:pl-60">
        <Topbar onMenuClick={() => setDrawerOpen(true)} />
        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
