import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listUsers } from './usersApi';
import type { TenantUser } from '@/types/user';

export const usersKey = ['users'] as const;

/* Tenant member directory. Cached for a minute because membership changes rarely within a session and
   several screens (interviewer picker, list avatars, detail panel) read the same list. */
export function useUsers() {
  return useQuery({ queryKey: usersKey, queryFn: listUsers, staleTime: 60_000 });
}

export function fullName(user: TenantUser): string {
  return `${user.firstName} ${user.lastName}`.trim();
}

/* A stable id → user lookup for resolving interviewer ids to names. Returns a Map so callers can do an
   O(1) lookup per interviewer instead of scanning the array (avoids an N×M render cost on the list). */
export function useUserLookup(): Map<string, TenantUser> {
  const { data } = useUsers();
  return useMemo(() => new Map((data ?? []).map((user) => [user.id, user])), [data]);
}
