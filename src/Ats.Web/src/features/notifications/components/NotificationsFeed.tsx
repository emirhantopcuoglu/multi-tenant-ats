import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, EmptyState, Pagination, Skeleton } from '@/components/ui';
import { cn } from '@/lib/cn';
import type { NotificationItem } from '../notificationsApi';
import { formatNotificationTime, renderNotification } from '../notificationText';
import type { candidateNotifications, companyNotifications } from '../useNotifications';

const PAGE_SIZE = 10;

interface NotificationsFeedProps {
  hooks: typeof candidateNotifications | typeof companyNotifications;
  /** Route prefix for an application deep-link, e.g. "/candidate/applications" or "/applications". */
  applicationBasePath: string;
  /** i18n key for the page subtitle — candidate and company copy differ ("your applications" vs
      "your team's hiring"), so the caller supplies it rather than this component picking a default. */
  subtitleKey: 'notifications.subtitle' | 'notifications.companySubtitle';
}

/* The full notification history behind a bell dropdown: same rows, plus pagination and a
   mark-all action. Shared by the candidate and company notifications pages — each page supplies
   its own hooks (which endpoint) and applicationBasePath (where a row should navigate), and owns
   its own outer layout (PublicLayout for the candidate portal, AppShell's <Outlet/> for company). */
export function NotificationsFeed({ hooks, applicationBasePath, subtitleKey }: NotificationsFeedProps) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);

  const query = hooks.useNotifications(page, PAGE_SIZE);
  const unreadQuery = hooks.useUnreadNotificationCount();
  const markRead = hooks.useMarkNotificationRead();
  const markAllRead = hooks.useMarkAllNotificationsRead();

  const items = query.data?.items ?? [];
  const totalPages = query.data?.totalPages ?? 1;
  const unreadCount = unreadQuery.data ?? 0;

  const openNotification = (item: NotificationItem) => {
    // Fire-and-forget: navigation must not wait on the read receipt.
    if (!item.readAtUtc) {
      markRead.mutate(item.id);
    }
    const { applicationId } = renderNotification(item, t, i18n.language);
    if (applicationId) {
      navigate(`${applicationBasePath}/${applicationId}`);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">{t('notifications.title')}</h1>
          <p className="text-sm text-text-muted">{t(subtitleKey)}</p>
        </div>
        {unreadCount > 0 && (
          <Button variant="secondary" onClick={() => markAllRead.mutate()}>
            {t('notifications.markAllRead')}
          </Button>
        )}
      </div>

      {query.isLoading ? (
        <div className="space-y-3" aria-busy="true">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : query.isError ? (
        <EmptyState
          title={t('notifications.loadError')}
          action={
            <button
              type="button"
              onClick={() => void query.refetch()}
              className="text-sm font-medium text-accent hover:underline"
            >
              {t('notifications.retry')}
            </button>
          }
        />
      ) : items.length === 0 ? (
        <EmptyState title={t('notifications.empty')} />
      ) : (
        <>
          <ul className="space-y-3">
            {items.map((item) => {
              const isUnread = item.readAtUtc === null;
              return (
                <li key={item.id}>
                  <Card padded={false}>
                    <button
                      type="button"
                      onClick={() => openNotification(item)}
                      className="flex w-full items-start gap-3 rounded-2xl p-4 text-left transition-colors hover:bg-divider"
                    >
                      <span
                        aria-hidden="true"
                        className={cn(
                          'mt-1.5 h-2 w-2 shrink-0 rounded-full',
                          isUnread ? 'bg-accent' : 'bg-transparent',
                        )}
                      />
                      <span className="min-w-0 flex-1 space-y-0.5">
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
                    </button>
                  </Card>
                </li>
              );
            })}
          </ul>

          {totalPages > 1 && (
            <div className="flex justify-center pt-2">
              <Pagination
                page={page}
                pageCount={totalPages}
                onPageChange={(p) => {
                  setPage(p);
                  window.scrollTo({ top: 0, behavior: 'smooth' });
                }}
              />
            </div>
          )}
        </>
      )}
    </div>
  );
}
