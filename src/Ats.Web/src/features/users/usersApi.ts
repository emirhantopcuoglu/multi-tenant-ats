import { apiClient, API_V1 } from '@/lib/apiClient';
import type { TenantUser } from '@/types/user';

/* GET /api/v1/users returns the caller's tenant members as a plain array (not paginated): the list is
   bounded by the tenant's size and every authenticated member may read it, so there is no filter or
   page parameter to mirror. */
export async function listUsers(): Promise<TenantUser[]> {
  const { data } = await apiClient.get<TenantUser[]>(`${API_V1}/users`);
  return data;
}
