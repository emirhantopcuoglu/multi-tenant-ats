import { useState } from 'react';
import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Skeleton } from '@/components/ui';
import { cn } from '@/lib/cn';
import type { NotificationItem } from '../notificationsApi';
import { formatNotificationTime, renderNotification } from '../notificationText';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
  useUnreadNotificationCount,
} from '../useNotifications';

/* The dropdown shows just enough to triage; the full page has pagination. */
const DROPDOWN_PAGE_SIZE = 5;
/* Past this the exact number stops mattering — cap the badge so it can't outgrow its circle. */
const MAX_BADGE_COUNT = 9;

function BellIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.73 21a2 2 0 0 1-3.46 0" />
    </svg>
  );
}

/* Bell + unread badge + recent-notifications feed for the signed-in candidate. Built directly on
   the Radix DropdownMenu primitive rather than the shared <Dropdown> wrapper: the wrapper's
   items-array API models an action menu, while this panel needs loading/empty/error states and a
   two-line row layout. Same primitive, same a11y guarantees — just a custom body.

   The list query only runs while the menu is open (`enabled`), so the always-mounted bell costs a
   single polled COUNT, not a feed fetch per page view. */
export function NotificationBell() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);

  const unreadQuery = useUnreadNotificationCount();
  const listQuery = useNotifications(1, DROPDOWN_PAGE_SIZE, isOpen);
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();

  const unreadCount = unreadQuery.data ?? 0;
  const badgeText =
    unreadCount > MAX_BADGE_COUNT ? `${MAX_BADGE_COUNT}+` : String(unreadCount);
  const items = listQuery.data?.items ?? [];

  const openNotification = (item: NotificationItem) => {
    // Fire-and-forget: navigation must not wait on the read receipt.
    if (!item.readAtUtc) {
      markRead.mutate(item.id);
    }
    const { applicationId } = renderNotification(item, t, i18n.language);
    navigate(
      applicationId ? `/candidate/applications/${applicationId}` : '/candidate/notifications',
    );
  };

  return (
    <DropdownMenu.Root open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          aria-label={t('notifications.bellLabel')}
          className="relative flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-bg text-text transition-colors hover:bg-divider"
        >
          <BellIcon />
          {unreadCount > 0 && (
            <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-accent px-1 text-[10px] font-semibold leading-none text-accent-fg">
              {badgeText}
            </span>
          )}
        </button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={6}
          className="z-50 w-80 rounded-xl border border-border bg-elevated p-1.5 shadow-card"
        >
          <div className="px-2.5 py-2 text-sm font-semibold text-text">
            {t('notifications.title')}
          </div>
          <DropdownMenu.Separator className="my-1.5 h-px bg-divider" />

          {listQuery.isLoading ? (
            <div className="space-y-2 px-2.5 py-2" aria-busy="true">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          ) : listQuery.isError ? (
            <p className="px-2.5 py-2 text-sm text-text-muted">{t('notifications.loadError')}</p>
          ) : items.length === 0 ? (
            <p className="px-2.5 py-2 text-sm text-text-muted">{t('notifications.empty')}</p>
          ) : (
            items.map((item) => {
              const isUnread = item.readAtUtc === null;
              return (
                <DropdownMenu.Item
                  key={item.id}
                  onSelect={() => openNotification(item)}
                  className="flex cursor-pointer items-start gap-2.5 rounded-lg px-2.5 py-2 outline-none transition-colors data-[highlighted]:bg-divider"
                >
                  <span
                    aria-hidden="true"
                    className={cn(
                      'mt-1.5 h-2 w-2 shrink-0 rounded-full',
                      isUnread ? 'bg-accent' : 'bg-transparent',
                    )}
                  />
                  <span className="min-w-0 space-y-0.5">
                    <span
                      className={cn(
                        'block text-sm leading-snug',
                        isUnread ? 'font-medium text-text' : 'text-text-muted',
                      )}
                    >
                      {renderNotification(item, t, i18n.language).text}
                    </span>
                    <span className="block text-xs text-text-muted">
                      {formatNotificationTime(item.createdAtUtc, i18n.language)}
                    </span>
                  </span>
                </DropdownMenu.Item>
              );
            })
          )}

          <DropdownMenu.Separator className="my-1.5 h-px bg-divider" />
          {unreadCount > 0 && (
            <DropdownMenu.Item
              // preventDefault keeps the menu open so the user sees the unread dots clear.
              onSelect={(event) => {
                event.preventDefault();
                markAllRead.mutate();
              }}
              className="cursor-pointer rounded-lg px-2.5 py-2 text-sm text-text outline-none transition-colors data-[highlighted]:bg-divider"
            >
              {t('notifications.markAllRead')}
            </DropdownMenu.Item>
          )}
          <DropdownMenu.Item
            onSelect={() => navigate('/candidate/notifications')}
            className="cursor-pointer rounded-lg px-2.5 py-2 text-sm font-medium text-accent outline-none transition-colors data-[highlighted]:bg-divider"
          >
            {t('notifications.viewAll')}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
