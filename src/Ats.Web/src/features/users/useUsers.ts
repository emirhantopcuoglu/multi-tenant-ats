import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listUsers } from './usersApi';
import type { TenantUser } from '@/types/user';

export const usersKey = ['users'] as const;

/* Cached for a minute: several screens read it and it rarely changes. */
export function useUsers() {
  return useQuery({ queryKey: usersKey, queryFn: listUsers, staleTime: 60_000 });
}

export function fullName(user: TenantUser): string {
  return `${user.firstName} ${user.lastName}`.trim();
}

/* A Map for O(1) lookups per interviewer. */
export function useUserLookup(): Map<string, TenantUser> {
  const { data } = useUsers();
  return useMemo(() => new Map((data ?? []).map((user) => [user.id, user])), [data]);
}
