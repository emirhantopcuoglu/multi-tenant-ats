import { apiClient, API_V1 } from '@/lib/apiClient';
import type { DashboardStats } from '@/types/dashboard';

export async function getDashboardStats(): Promise<DashboardStats> {
  const { data } = await apiClient.get<DashboardStats>(`${API_V1}/dashboard/stats`);
  return data;
}
