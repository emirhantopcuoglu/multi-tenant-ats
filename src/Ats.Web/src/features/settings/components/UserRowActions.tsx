import { useTranslation } from 'react-i18next';
import { Dropdown, IconButton, type DropdownAction } from '@/components/ui';
import { ROLES } from '@/types/enums';
import type { TenantUser } from '@/types/user';

export type UserAction = { kind: 'role'; role: TenantUser['role'] } | { kind: 'deactivate' } | { kind: 'reactivate' };

interface UserRowActionsProps {
  user: TenantUser;
  /** The signed-in admin's own id. They may not change their own role or deactivate themselves — the
   *  backend refuses both, so offering them would only produce an error the UI has to explain. */
  currentUserId: string | undefined;
  onAction: (user: TenantUser, action: UserAction) => void;
}

function KebabIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <circle cx="12" cy="5" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="12" cy="19" r="1.6" />
    </svg>
  );
}

/* Per-row menu for a team member. Items mirror the backend's rules so the UI never offers an action
   that is certain to fail: no self-targeting, no role change on a deactivated member (reactivate them
   first), and the member's current role is shown disabled rather than hidden so the menu reads as a
   set of choices with one already taken.

   The last-admin rule is NOT mirrored here. It depends on how many other active admins exist, which
   this row does not know, and duplicating that count client-side is exactly how a UI drifts out of
   sync with the rule it is copying. That one surfaces as a toast from the server's answer. */
export function UserRowActions({ user, currentUserId, onAction }: UserRowActionsProps) {
  const { t } = useTranslation();
  const isSelf = user.id === currentUserId;

  const items: DropdownAction[] = [];

  if (!isSelf && user.isActive) {
    for (const role of ROLES) {
      items.push({
        key: `role-${role}`,
        label: t('settings.users.makeRole', { role: t(`role.${role}`) }),
        disabled: role === user.role,
        onSelect: () => onAction(user, { kind: 'role', role }),
      });
    }
  }

  if (!isSelf) {
    items.push(
      user.isActive
        ? {
            key: 'deactivate',
            label: t('settings.users.deactivate'),
            tone: 'danger',
            separatorBefore: items.length > 0,
            onSelect: () => onAction(user, { kind: 'deactivate' }),
          }
        : {
            key: 'reactivate',
            label: t('settings.users.reactivate'),
            onSelect: () => onAction(user, { kind: 'reactivate' }),
          },
    );
  }

  if (items.length === 0) return null;

  return (
    <Dropdown
      align="end"
      items={items}
      trigger={<IconButton aria-label={t('settings.users.rowActions')} icon={<KebabIcon />} />}
    />
  );
}
