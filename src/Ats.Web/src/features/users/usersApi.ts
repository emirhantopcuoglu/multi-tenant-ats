import { apiClient, API_V1 } from '@/lib/apiClient';
import type { TenantUser } from '@/types/user';
import type { Role } from '@/types/enums';

/* A plain array, not paged: bounded by the tenant's size. */
export async function listUsers(): Promise<TenantUser[]> {
  const { data } = await apiClient.get<TenantUser[]>(`${API_V1}/users`);
  return data;
}

export async function changeUserRole(userId: string, role: Role): Promise<void> {
  await apiClient.put(`${API_V1}/users/${userId}/role`, { role });
}

export async function deactivateUser(userId: string): Promise<void> {
  await apiClient.post(`${API_V1}/users/${userId}/deactivate`);
}

export async function reactivateUser(userId: string): Promise<void> {
  await apiClient.post(`${API_V1}/users/${userId}/reactivate`);
}
