import { useMutation, useQueryClient } from '@tanstack/react-query';
import { inviteUser } from './invitationsApi';
import { usersKey } from '@/features/users/useUsers';

/* Invite mutation. An accepted invitation adds a tenant member, so on success we invalidate the
   users list — the new member surfaces there once they accept. The modal owns the toast and the
   code → message mapping for the invite-specific errors. */
export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: inviteUser,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: usersKey }),
  });
}
