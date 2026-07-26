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
  useToast,
} from '@/components/ui';
import { useAuth } from '@/app/auth/auth-context';
import { roleTone } from '@/lib/statusColors';
import { toApiError } from '@/lib/problemDetails';
import { fullName, useUsers } from '@/features/users/useUsers';
import {
  useChangeUserRole,
  useDeactivateUser,
  useReactivateUser,
} from '@/features/users/useUserManagement';
import { InviteUserModal } from './InviteUserModal';
import { UserRowActions, type UserAction } from './UserRowActions';

/* Server-side rules that deserve their own message rather than the generic failure. Both are refusals
   the row cannot predict — the last-admin count is not in the client's hands — so they arrive as an
   error and are translated here. */
const LAST_ADMIN_CODE = 'user_management.last_admin';
const CANNOT_TARGET_SELF_CODE = 'user_management.cannot_target_self';

/* Users tab. Lists the tenant's members, opens the invite modal, and (for an Admin) changes a
   member's role or revokes their access. The list reuses the shared `useUsers` query — the same one
   the interviewer picker reads — so the mutations invalidate one cache and every screen updates. */
export function UsersTab() {
  const { t } = useTranslation();
  const { user: currentUser } = useAuth();
  const { toast } = useToast();
  const usersQuery = useUsers();
  const [isInviteOpen, setIsInviteOpen] = useState(false);

  const changeRole = useChangeUserRole();
  const deactivate = useDeactivateUser();
  const reactivate = useReactivateUser();

  const users = usersQuery.data ?? [];
  const isAdmin = currentUser?.kind === 'company' && currentUser.role === 'Admin';

  const notifyFailure = (error: unknown) => {
    const { code } = toApiError(error);
    const key =
      code === LAST_ADMIN_CODE
        ? 'settings.users.lastAdminError'
        : code === CANNOT_TARGET_SELF_CODE
          ? 'settings.users.selfError'
          : 'settings.users.actionError';
    toast({ title: t(key), tone: 'danger' });
  };

  const handleAction = (user: (typeof users)[number], action: UserAction) => {
    // The key is constrained to the three literals rather than `string`: `t` is typed against the
    // real translation keys, so a widened parameter would defeat the check that they exist.
    type SuccessKey =
      | 'settings.users.roleChanged'
      | 'settings.users.deactivated'
      | 'settings.users.reactivated';

    const done = (titleKey: SuccessKey) => ({
      onSuccess: () => toast({ title: t(titleKey), tone: 'success' as const }),
      onError: notifyFailure,
    });

    if (action.kind === 'role') {
      changeRole.mutate({ userId: user.id, role: action.role }, done('settings.users.roleChanged'));
      return;
    }
    if (action.kind === 'deactivate') {
      deactivate.mutate(user.id, done('settings.users.deactivated'));
      return;
    }
    reactivate.mutate(user.id, done('settings.users.reactivated'));
  };

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
              <TH>{t('settings.users.status')}</TH>
              {isAdmin && <TH className="w-10" />}
            </TR>
          </THead>
          <TBody>
            {users.map((user) => (
              <TR key={user.id} className={user.isActive ? undefined : 'opacity-60'}>
                <TD>
                  <div className="flex items-center gap-2.5">
                    <Avatar name={fullName(user)} />
                    <span className="font-medium text-text">{fullName(user)}</span>
                    {user.id === currentUser?.id && (
                      <span className="text-xs text-text-muted">{t('settings.users.you')}</span>
                    )}
                  </div>
                </TD>
                <TD className="text-text-muted">{user.email}</TD>
                <TD>
                  <Badge tone={roleTone[user.role]}>{t(`role.${user.role}`)}</Badge>
                </TD>
                <TD>
                  <Badge tone={user.isActive ? 'success' : 'gray'} dot>
                    {t(user.isActive ? 'settings.users.active' : 'settings.users.inactive')}
                  </Badge>
                </TD>
                {isAdmin && (
                  <TD>
                    <UserRowActions
                      user={user}
                      currentUserId={currentUser?.id}
                      onAction={handleAction}
                    />
                  </TD>
                )}
              </TR>
            ))}
          </TBody>
        </Table>
      )}

      <InviteUserModal open={isInviteOpen} onOpenChange={setIsInviteOpen} />
    </Card>
  );
}
