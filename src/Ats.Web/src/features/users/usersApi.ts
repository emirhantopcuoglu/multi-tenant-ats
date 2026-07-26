import { apiClient, API_V1 } from '@/lib/apiClient';
import type { TenantUser } from '@/types/user';
import type { Role } from '@/types/enums';

/* GET /api/v1/users returns the caller's tenant members as a plain array (not paginated): the list is
   bounded by the tenant's size and every authenticated member may read it, so there is no filter or
   page parameter to mirror. */
export async function listUsers(): Promise<TenantUser[]> {
  const { data } = await apiClient.get<TenantUser[]>(`${API_V1}/users`);
  return data;
}

/* Admin-only mutations. All three return 204, so there is nothing to unwrap; callers invalidate the
   users query afterwards to pick up the new state. */
export async function changeUserRole(userId: string, role: Role): Promise<void> {
  await apiClient.put(`${API_V1}/users/${userId}/role`, { role });
}

export async function deactivateUser(userId: string): Promise<void> {
  await apiClient.post(`${API_V1}/users/${userId}/deactivate`);
}

export async function reactivateUser(userId: string): Promise<void> {
  await apiClient.post(`${API_V1}/users/${userId}/reactivate`);
}
