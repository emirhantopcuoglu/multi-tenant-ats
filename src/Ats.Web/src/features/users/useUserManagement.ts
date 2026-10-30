import { useMutation, useQueryClient } from '@tanstack/react-query';
import { changeUserRole, deactivateUser, reactivateUser } from './usersApi';
import { usersKey } from './useUsers';
import type { Role } from '@/types/enums';

/* Each invalidates the shared users query, so every screen updates. */
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
