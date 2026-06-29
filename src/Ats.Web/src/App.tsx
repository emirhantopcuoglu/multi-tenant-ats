import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '@/app/auth/RequireAuth';
import { RequireRole } from '@/app/auth/RequireRole';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { AcceptInvitationPage } from '@/features/auth/pages/AcceptInvitationPage';
import { HomePage } from '@/features/home/HomePage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { PlaygroundPage } from '@/features/playground/PlaygroundPage';

/* Router skeleton (Step 2.1). Public auth routes sit at the top; everything else is nested under
   RequireAuth, with role-restricted areas nested again under RequireRole. The real app shell + screens
   fill in from Step 2.3 onward; /playground stays reachable as a dev gallery until then. */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/accept-invitation" element={<AcceptInvitationPage />} />
      <Route path="/playground" element={<PlaygroundPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/" element={<HomePage />} />
        <Route element={<RequireRole roles={['Admin']} />}>
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Route>

      {/* Unknown paths fall back to the protected root, which itself redirects to /login when anonymous. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
