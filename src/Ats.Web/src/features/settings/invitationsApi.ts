import { apiClient, API_V1 } from '@/lib/apiClient';
import type { InviteUserRequest } from '@/types/invitation';

export async function inviteUser(request: InviteUserRequest): Promise<void> {
  await apiClient.post(`${API_V1}/invitations`, request);
}
