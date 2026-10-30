import { useTranslation } from 'react-i18next';
import { Dropdown, IconButton, type DropdownAction } from '@/components/ui';
import { ROLES } from '@/types/enums';
import type { TenantUser } from '@/types/user';

export type UserAction = { kind: 'role'; role: TenantUser['role'] } | { kind: 'deactivate' } | { kind: 'reactivate' };

interface UserRowActionsProps {
  user: TenantUser;
  /* The backend refuses self-changes, so they aren't offered. */
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

/* Mirrors the backend rules: no self-targeting, no role change on a deactivated member. The
   last-admin rule isn't mirrored (the row doesn't know the count); the server's error shows as a toast. */
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
