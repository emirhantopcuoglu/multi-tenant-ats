import { apiClient, API_V1 } from '@/lib/apiClient';
import type { InviteUserRequest } from '@/types/invitation';

/* POST /api/v1/invitations sends an invitation email. It returns 200 with no body on success, or a
   structured { code, message } error (e.g. invite.email_in_use) the modal maps to a message. */
export async function inviteUser(request: InviteUserRequest): Promise<void> {
  await apiClient.post(`${API_V1}/invitations`, request);
}
