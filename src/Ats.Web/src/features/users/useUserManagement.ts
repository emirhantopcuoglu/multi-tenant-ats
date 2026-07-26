import { useMutation, useQueryClient } from '@tanstack/react-query';
import { changeUserRole, deactivateUser, reactivateUser } from './usersApi';
import { usersKey } from './useUsers';
import type { Role } from '@/types/enums';

/* Admin-only member mutations. Each invalidates the shared users query on success, so Settings, the
   interviewer picker and the list avatars all pick up the change from one refetch rather than each
   keeping its own copy in sync. */
export function useChangeUserRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: Role }) => changeUserRole(userId, role),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: usersKey }),
  });
}

export function useDeactivateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => deactivateUser(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: usersKey }),
  });
}

export function useReactivateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => reactivateUser(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: usersKey }),
  });
}
