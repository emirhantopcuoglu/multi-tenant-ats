import { Card } from '@/components/ui';

/* Placeholder settings route, mounted behind RequireRole(['Admin']) in the router so the role guard
   has a concrete target. The real Settings screen (company + users) arrives in Step 4.2. */
export function SettingsPage() {
  return (
    <div className="mx-auto max-w-2xl p-6">
      <Card className="space-y-1">
        <h1 className="text-lg font-semibold text-text">Settings</h1>
        <p className="text-sm text-text-muted">Admin-only. The real settings screen arrives later.</p>
      </Card>
    </div>
  );
}
