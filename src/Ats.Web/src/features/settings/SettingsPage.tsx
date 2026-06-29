import { Card } from '@/components/ui';

/* Placeholder settings route, mounted behind RequireRole(['Admin']) inside the app shell so the role
   guard has a concrete target. The shell provides the page padding, so this only owns its content.
   The real Settings screen (company + users) arrives in Step 4.2. */
export function SettingsPage() {
  return (
    <div className="mx-auto max-w-2xl">
      <Card className="space-y-1">
        <h2 className="text-lg font-semibold text-text">Settings</h2>
        <p className="text-sm text-text-muted">Admin-only. The real settings screen arrives later.</p>
      </Card>
    </div>
  );
}
