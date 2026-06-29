import { useSearchParams } from 'react-router-dom';
import { Card } from '@/components/ui';

/* Placeholder invitation-accept route. The token travels in the URL query (?token=...); the form
   that POSTs it to /invitations/accept arrives in Step 2.2. We read the token now only to confirm
   the route wiring. */
export function AcceptInvitationPage() {
  const [searchParams] = useSearchParams();
  const hasToken = searchParams.get('token') !== null;

  return (
    <div className="flex min-h-screen items-center justify-center bg-bg p-6">
      <Card className="w-full max-w-sm space-y-3 text-center">
        <h1 className="text-xl font-semibold text-text">Accept your invitation</h1>
        <p className="text-sm text-text-muted">
          {hasToken
            ? 'The accept form arrives in the next step.'
            : 'This invitation link is missing its token.'}
        </p>
      </Card>
    </div>
  );
}
