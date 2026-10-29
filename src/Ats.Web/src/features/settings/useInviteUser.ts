import { useMutation, useQueryClient } from '@tanstack/react-query';
import { inviteUser } from './invitationsApi';
import { usersKey } from '@/features/users/useUsers';

/* The modal handles the toast and error messages. */
export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: inviteUser,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: usersKey }),
  });
}
