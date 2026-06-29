import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '@/app/auth/RequireAuth';
import { RequireRole } from '@/app/auth/RequireRole';
import { AppShell } from '@/components/layout/AppShell';
import { PagePlaceholder } from '@/components/layout/PagePlaceholder';
import { JobsPage } from '@/features/jobs/JobsPage';
import { JobFormPage } from '@/features/jobs/JobFormPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { AcceptInvitationPage } from '@/features/auth/pages/AcceptInvitationPage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { PlaygroundPage } from '@/features/playground/PlaygroundPage';

/* Public auth routes sit at the top. Everything else is nested under RequireAuth → AppShell, so the
   shell (sidebar + topbar) wraps every authenticated screen and the routed page renders through its
   <Outlet/>. Role-restricted areas nest again under RequireRole. Real feature screens replace the
   placeholders from Phase 3 onward; /playground stays a public dev gallery for now. */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/accept-invitation" element={<AcceptInvitationPage />} />
      <Route path="/playground" element={<PlaygroundPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<PagePlaceholder titleKey="nav.overview" />} />
          <Route path="/jobs" element={<JobsPage />} />
          <Route path="/jobs/new" element={<JobFormPage />} />
          <Route path="/jobs/:id/edit" element={<JobFormPage />} />
          <Route path="/applications" element={<PagePlaceholder titleKey="nav.applications" />} />
          <Route path="/interviews" element={<PagePlaceholder titleKey="nav.interviews" />} />
          <Route path="/candidates" element={<PagePlaceholder titleKey="nav.candidates" />} />
          <Route element={<RequireRole roles={['Admin']} />}>
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Route>
      </Route>

      {/* Unknown paths fall back to the protected root, which itself redirects to /login when anonymous. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
