import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from '@/app/auth/RequireAuth';
import { RequireCandidateAuth } from '@/app/auth/RequireCandidateAuth';
import { RequireActiveCandidate } from '@/app/auth/RequireActiveCandidate';
import { RequireRole } from '@/app/auth/RequireRole';
import { AppShell } from '@/components/layout/AppShell';
import { CandidateSearchPage } from '@/features/candidate-search/CandidateSearchPage';
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
import { ForgotPasswordPage } from '@/features/auth/pages/ForgotPasswordPage';
import { ResetPasswordPage } from '@/features/auth/pages/ResetPasswordPage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { MarketplacePage } from '@/features/public/MarketplacePage';
import { PublicCareersPage } from '@/features/public/PublicCareersPage';
import { PublicJobDetailPage } from '@/features/public/PublicJobDetailPage';
import { PublicApplyPage } from '@/features/public/PublicApplyPage';
import { PlaygroundPage } from '@/features/playground/PlaygroundPage';
import { CandidateLoginPage } from '@/features/candidates/pages/CandidateLoginPage';
import { CandidateRegisterPage } from '@/features/candidates/pages/CandidateRegisterPage';
import { ConfirmEmailChangePage } from '@/features/candidates/pages/ConfirmEmailChangePage';
import { CandidateForgotPasswordPage } from '@/features/candidates/pages/CandidateForgotPasswordPage';
import { CandidateResetPasswordPage } from '@/features/candidates/pages/CandidateResetPasswordPage';
import { CandidateVerifyEmailPage } from '@/features/candidates/pages/CandidateVerifyEmailPage';
import { CandidateApplicationsPage } from '@/features/candidates/pages/CandidateApplicationsPage';
import { CandidateApplicationDetailPage } from '@/features/candidates/pages/CandidateApplicationDetailPage';
import { CandidateInterviewsPage } from '@/features/candidates/pages/CandidateInterviewsPage';
import { InterviewRoomPage } from '@/features/interview-room/InterviewRoomPage';
import { CandidateNotificationsPage } from '@/features/notifications/pages/CandidateNotificationsPage';
import { CandidateSettingsPage } from '@/features/candidates/pages/CandidateSettingsPage';
import { CandidateProfileSettingsTab } from '@/features/candidates/pages/CandidateProfileSettingsTab';
import { CandidateSecuritySettingsTab } from '@/features/candidates/pages/CandidateSecuritySettingsTab';
import { CandidateAccountSettingsTab } from '@/features/candidates/pages/CandidateAccountSettingsTab';
import { CandidateReactivatePage } from '@/features/candidates/pages/CandidateReactivatePage';
import { CompanyNotificationsPage } from '@/features/notifications/pages/CompanyNotificationsPage';

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
      {/* Recovery, necessarily anonymous. Both paths are reserved in SlugPolicy so a company slug
          can never shadow them. */}
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/candidate/login" element={<CandidateLoginPage />} />
      <Route path="/candidate/register" element={<CandidateRegisterPage />} />
      {/* Public like /accept-invitation: the mailed link may be opened without a session. */}
      <Route path="/candidate/confirm-email" element={<ConfirmEmailChangePage />} />
      {/* Recovery, necessarily anonymous — whoever needs these cannot sign in. */}
      {/* Anonymous like the reset-password route below: opened from an email client, which carries no
          session. The token in the query string is the credential. */}
      <Route path="/candidate/verify-email" element={<CandidateVerifyEmailPage />} />
      <Route path="/candidate/forgot-password" element={<CandidateForgotPasswordPage />} />
      <Route path="/candidate/reset-password" element={<CandidateResetPasswordPage />} />
      <Route path="/playground" element={<PlaygroundPage />} />
      {/* Reachable by either a candidate or a company interviewer session — the page itself checks
          which, since neither RequireAuth nor RequireCandidateAuth alone would fit both. */}
      <Route path="/interview-room/:roomToken" element={<InterviewRoomPage />} />

      <Route element={<RequireCandidateAuth />}>
        {/* Outside RequireActiveCandidate on purpose: it is the one screen a frozen account may see. */}
        <Route path="/candidate/reactivate" element={<CandidateReactivatePage />} />
        <Route element={<RequireActiveCandidate />}>
          <Route path="/candidate/applications" element={<CandidateApplicationsPage />} />
          <Route path="/candidate/applications/:id" element={<CandidateApplicationDetailPage />} />
          <Route path="/candidate/interviews" element={<CandidateInterviewsPage />} />
          <Route path="/candidate/notifications" element={<CandidateNotificationsPage />} />
          <Route path="/candidate/settings" element={<CandidateSettingsPage />}>
            <Route index element={<Navigate to="profile" replace />} />
            <Route path="profile" element={<CandidateProfileSettingsTab />} />
            <Route path="security" element={<CandidateSecuritySettingsTab />} />
            <Route path="account" element={<CandidateAccountSettingsTab />} />
          </Route>
        </Route>
      </Route>

      {/* Anonymous careers pages. Static routes above (e.g. /login, /jobs) outrank these dynamic
          single-segment patterns, so they only match a tenant slug, never an app path. */}
      <Route path="/:slug" element={<PublicCareersPage />} />
      <Route path="/:slug/jobs/:jobSlug" element={<PublicJobDetailPage />} />
      <Route path="/:slug/jobs/:jobSlug/apply" element={<PublicApplyPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/dashboard" element={<OverviewPage />} />
          <Route path="/notifications" element={<CompanyNotificationsPage />} />
          <Route path="/jobs" element={<JobsPage />} />
          <Route path="/jobs/new" element={<JobFormPage />} />
          <Route path="/jobs/:id/edit" element={<JobFormPage />} />
          <Route path="/applications" element={<ApplicationsPage />} />
          <Route path="/applications/:id" element={<ApplicationDetailPage />} />
          <Route path="/interviews" element={<InterviewsPage />} />
          <Route path="/interviews/:id" element={<InterviewDetailPage />} />
          <Route path="/candidates" element={<CandidateSearchPage />} />
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
