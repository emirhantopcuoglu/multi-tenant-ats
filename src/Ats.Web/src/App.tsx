import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '@/app/auth/RequireAuth';
import { RequireRole } from '@/app/auth/RequireRole';
import { AppShell } from '@/components/layout/AppShell';
import { PagePlaceholder } from '@/components/layout/PagePlaceholder';
import { JobsPage } from '@/features/jobs/JobsPage';
import { JobFormPage } from '@/features/jobs/JobFormPage';
import { ApplicationsPage } from '@/features/applications/ApplicationsPage';
import { ApplicationDetailPage } from '@/features/applications/ApplicationDetailPage';
import { InterviewsPage } from '@/features/interviews/InterviewsPage';
import { InterviewDetailPage } from '@/features/interviews/InterviewDetailPage';
import { OverviewPage } from '@/features/dashboard/OverviewPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { AcceptInvitationPage } from '@/features/auth/pages/AcceptInvitationPage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { MarketplacePage } from '@/features/public/MarketplacePage';
import { PublicCareersPage } from '@/features/public/PublicCareersPage';
import { PublicJobDetailPage } from '@/features/public/PublicJobDetailPage';
import { PublicApplyPage } from '@/features/public/PublicApplyPage';
import { PlaygroundPage } from '@/features/playground/PlaygroundPage';
import { CandidateLoginPage } from '@/features/candidates/pages/CandidateLoginPage';
import { CandidateRegisterPage } from '@/features/candidates/pages/CandidateRegisterPage';

/* Public auth routes sit at the top. Everything else is nested under RequireAuth → AppShell, so the
   shell (sidebar + topbar) wraps every authenticated screen and the routed page renders through its
   <Outlet/>. Role-restricted areas nest again under RequireRole. Real feature screens replace the
   placeholders from Phase 3 onward; /playground stays a public dev gallery for now. */
export default function App() {
  return (
    <Routes>
      {/* The root is the public marketplace — visible to everyone, auth or not. */}
      <Route path="/" element={<MarketplacePage />} />

      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/accept-invitation" element={<AcceptInvitationPage />} />
      <Route path="/candidate/login" element={<CandidateLoginPage />} />
      <Route path="/candidate/register" element={<CandidateRegisterPage />} />
      <Route path="/playground" element={<PlaygroundPage />} />

      {/* Anonymous careers pages. Static routes above (e.g. /login, /jobs) outrank these dynamic
          single-segment patterns, so they only match a tenant slug, never an app path. */}
      <Route path="/:slug" element={<PublicCareersPage />} />
      <Route path="/:slug/jobs/:jobSlug" element={<PublicJobDetailPage />} />
      <Route path="/:slug/jobs/:jobSlug/apply" element={<PublicApplyPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/dashboard" element={<OverviewPage />} />
          <Route path="/jobs" element={<JobsPage />} />
          <Route path="/jobs/new" element={<JobFormPage />} />
          <Route path="/jobs/:id/edit" element={<JobFormPage />} />
          <Route path="/applications" element={<ApplicationsPage />} />
          <Route path="/applications/:id" element={<ApplicationDetailPage />} />
          <Route path="/interviews" element={<InterviewsPage />} />
          <Route path="/interviews/:id" element={<InterviewDetailPage />} />
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
