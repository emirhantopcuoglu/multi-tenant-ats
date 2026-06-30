import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Avatar,
  Badge,
  Button,
  Card,
  EmptyState,
  Skeleton,
  Table,
  THead,
  TBody,
  TR,
  TH,
  TD,
} from '@/components/ui';
import { roleTone } from '@/lib/statusColors';
import { fullName, useUsers } from '@/features/users/useUsers';
import { InviteUserModal } from './InviteUserModal';

/* Users tab. Lists the tenant's members and opens the invite modal. The list reuses the shared
   `useUsers` query (same one the interviewer picker reads), so opening Settings doesn't refetch what
   other screens already cached. */
export function UsersTab() {
  const { t } = useTranslation();
  const usersQuery = useUsers();
  const [isInviteOpen, setIsInviteOpen] = useState(false);

  const users = usersQuery.data ?? [];

  return (
    <Card className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold text-text">{t('settings.users.title')}</h2>
        <Button onClick={() => setIsInviteOpen(true)}>{t('settings.users.invite')}</Button>
      </div>

      {usersQuery.isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : usersQuery.isError ? (
        <EmptyState title={t('settings.users.loadError')} />
      ) : users.length === 0 ? (
        <EmptyState title={t('settings.users.empty')} />
      ) : (
        <Table>
          <THead>
            <TR>
              <TH>{t('settings.users.name')}</TH>
              <TH>{t('settings.users.email')}</TH>
              <TH>{t('settings.users.role')}</TH>
            </TR>
          </THead>
          <TBody>
            {users.map((user) => (
              <TR key={user.id}>
                <TD>
                  <div className="flex items-center gap-2.5">
                    <Avatar name={fullName(user)} />
                    <span className="font-medium text-text">{fullName(user)}</span>
                  </div>
                </TD>
                <TD className="text-text-muted">{user.email}</TD>
                <TD>
                  <Badge tone={roleTone[user.role]}>{t(`role.${user.role}`)}</Badge>
                </TD>
              </TR>
            ))}
          </TBody>
        </Table>
      )}

      <InviteUserModal open={isInviteOpen} onOpenChange={setIsInviteOpen} />
    </Card>
  );
}
