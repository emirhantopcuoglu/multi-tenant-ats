# Improvement Roadmap — Candidate Experience & Platform Gaps

Source: user-reported problems (2026-07-04) plus findings from a code audit.
Each step below is one branch + one PR into `develop`, small enough to review in one sitting.
Every step ends with a ready-to-use prompt that carries its own context, so work can start
from a fresh session without re-explaining anything.

## Problem → step mapping

| # | Reported problem | Step(s) |
|---|------------------|---------|
| 1 | Candidate/company auth split is confusing, not visible on the home page | 1.1 |
| 2 | Applied jobs don't show an "applied" state; re-apply form is reachable | 1.2 |
| 3 | Job pages are weak; no company info or company profile page | 2.1, 2.2 |
| 4 | Apply form inputs (phone) have no validation | 1.3 |
| 5 | Is the AI CV analysis actually working? | 0.1 |
| 6 | No notification to the candidate on stage change / review | 3.1–3.4 |
| 7 | Candidate never learns an interview was scheduled | 3.1, 3.4, 5.1 |
| 8 | Home page is empty | 2.3 |
| 9 | Live interview UI is missing | 5.1, 5.2 |
| 10 | No candidate profile page | 4.1 |

## Additional findings from the audit

- `/candidate/applications` is registered as a public route in `App.tsx` — the page self-guards,
  but there is no `RequireCandidateAuth` route wrapper like the company side has (folded into 4.1).
- Candidate sessions have a single access token and no refresh flow, so the session dies abruptly
  when the token expires (folded into 4.1 as a decision point).
- `/candidates` in the company app is still a `PagePlaceholder`.
- Marketplace has no filters (location, employment type) — folded into 2.3.
- Notification emails are English-only while the UI is bilingual (accepted for MVP; revisit later).
- The repo has no `README.md` (a CLAUDE.md requirement) — step 0.2.

---

## Phase 0 — Verification & hygiene

### 0.1 Verify the AI CV parsing pipeline end to end

**Why first:** it answers an open question ("does it work?") before anything is built on top of it.
No code is written unless something is broken.

**What it covers:** submit a real application with a PDF CV against the local stack, follow
`CvParseRequestedIntegrationEvent` → `CvParsingConsumer` (MinIO download → PdfPig text extraction →
LLM call via the GitHub Models provider → MongoDB upsert), then confirm the result renders in the
recruiter's `CvAnalysisTab`. Check RabbitMQ dead-letter queues and API logs for failures.

**Prompt:**
> Verify the AI CV parsing pipeline end to end on the local stack (docker infra + API on 5236 +
> Vite on 5173). Register a candidate, apply to a published job with a real PDF CV, then confirm:
> the CvParseRequested event is consumed, text is extracted, the LLM (GitHub Models, `Llm` config
> section) returns structured data, the result lands in MongoDB, and the recruiter sees it in the
> application detail CV analysis tab. Report what works and what doesn't; fix only clear bugs, on a
> `fix/*` branch.

### 0.2 Add a README

**Prompt:**
> Write `README.md` at the repo root: what Ats is, architecture overview (modular monolith +
> React SPA), local setup (docker compose infra, user-secrets, migrations, API + web dev servers),
> how to run tests, and the release flow (release-please). Keep it under ~150 lines. Branch
> `docs/readme`, PR into develop.

---

## Phase 1 — Correctness & UX quick wins

### 1.1 Separate candidate and company entry points

**Problem:** the public header only offers candidate sign-in/register; company users have no path
from the home page, and the login/register pages don't say who they are for.

**Scope (frontend only):**
- `PublicLayout` header: keep candidate CTAs, add a quiet "For companies" link to `/login`.
- Marketplace page: add a "for companies" band (post jobs → `/register`).
- `LoginPage` / `RegisterPage`: label them as company screens; cross-link to candidate auth.
- `CandidateLoginPage` / `CandidateRegisterPage`: cross-link to company auth.
- All strings via i18n (en + tr).

**Prompt:**
> Implement roadmap step 1.1 (docs/improvement-roadmap.md): make the candidate/company auth split
> obvious. PublicLayout header gets a "For companies" link to /login; MarketplacePage gets a small
> for-companies section linking to /register; company Login/Register pages get a subtitle marking
> them as company screens plus a cross-link to /candidate/login, and vice versa on the candidate
> pages. i18n for all new strings (en/tr). Branch `feature/web-auth-entry-split`, PR into develop.

### 1.2 Show "applied" state on jobs the candidate already applied to

**Problem:** after applying, the job detail and apply pages still show a fresh apply flow; the
duplicate is only caught server-side at submit.

**Scope:**
- Backend (Applications module): `GET /api/v1/candidate/applications/job-ids` returning the job ids
  the current candidate has applied to (CandidateOnly policy). Cheap, unpaged by design — a
  candidate's own application count is naturally small.
- Frontend: a `useAppliedJobIds` query (enabled only for candidate sessions). `PublicJobDetailPage`
  shows an "Applied" badge and swaps the apply CTA for a link to `/candidate/applications`;
  `PublicApplyPage` short-circuits to an "already applied" card; `CandidateApplicationsPage` rows
  link to the job detail page (no re-apply affordance).

**Prompt:**
> Implement roadmap step 1.2 (docs/improvement-roadmap.md): applied-state visibility. Add a
> CandidateOnly endpoint GET /api/v1/candidate/applications/job-ids returning the current
> candidate's applied job ids (Applications module, MediatR query, integration test). In the SPA
> add a useAppliedJobIds hook (React Query, candidate sessions only); PublicJobDetailPage shows an
> Applied badge + link to my applications instead of the apply button; PublicApplyPage renders an
> already-applied card instead of the form; CandidateApplicationsPage links each row to the public
> job detail. i18n en/tr. Branch `feature/candidate-applied-state`, PR into develop.

### 1.3 Real validation on the apply form

**Problem:** phone/LinkedIn/cover letter fields are raw `useState` inputs with zero validation;
the backend rules are the only gate.

**Scope:**
- Frontend: migrate `PublicApplyPage` to react-hook-form + zod like the auth pages. Phone:
  optional, but if present must match a lenient international pattern (`+` plus 7–15 digits,
  spaces/dashes tolerated then normalized). LinkedIn: optional, must be a valid https URL on
  linkedin.com. Cover letter: max length. Field-level errors through the existing `Field` component.
- Backend: verify the Apply command's FluentValidation rules cover the same constraints; add what's
  missing so the API stays the source of truth.

**Prompt:**
> Implement roadmap step 1.3 (docs/improvement-roadmap.md): apply-form validation. Rewrite
> PublicApplyPage's form with react-hook-form + zod (pattern: LoginPage). Phone optional but
> validated (+ and 7–15 digits after stripping spaces/dashes, normalized before submit), LinkedIn
> optional but must be a valid https linkedin.com URL, cover letter max 4000 chars. Mirror the same
> rules in the backend Apply command validator if missing, with tests. i18n en/tr for new messages.
> Branch `feature/apply-form-validation`, PR into develop.

---

## Phase 2 — Content pages

### 2.1 Company public profile

**Problem:** companies are just a name + slug; there is nothing to look at.

**Scope:**
- Tenants module: add profile fields to the tenant (description, website, location; logo deferred —
  needs upload plumbing). EF migration. Admin can edit them in Settings → Company tab.
- Public surface: extend the public company endpoint/DTO; `PublicCareersPage` renders the profile
  above the job list.

**Prompt:**
> Implement roadmap step 2.1 (docs/improvement-roadmap.md): company public profile. Tenants module:
> add Description, WebsiteUrl, Location to the tenant entity + EF migration; extend the update
> company command and the Settings CompanyTab form. Extend the public companies endpoint DTO and
> render the profile block (name, description, website link, location) on PublicCareersPage above
> the jobs. Validation: website must be a valid http(s) URL; description max 2000 chars. Tests for
> the command/query changes. i18n en/tr. Branch `feature/company-public-profile`, PR into develop.

### 2.2 Richer public job detail page

**Problem:** the job detail page shows the posting but no employer context.

**Scope (frontend-heavy):** company card (name, location, description snippet, link to `/:slug`),
posting metadata (posted date, employment type, experience level, location as a proper meta row),
sticky/clear apply CTA that respects the 1.2 applied state, and "more jobs from this company".

**Prompt:**
> Implement roadmap step 2.2 (docs/improvement-roadmap.md): enrich PublicJobDetailPage. Add a
> company card (data from the public company endpoint added in 2.1, linking to the careers page),
> a metadata row (posted date, employment type, experience level, location), and a "more jobs from
> this company" list (existing public jobs endpoint, exclude current job, cap at 3). Apply CTA
> respects the applied state from 1.2. i18n en/tr. Branch `feature/public-job-detail-v2`, PR into
> develop.

### 2.3 Marketplace home page redesign

**Problem:** the home page is a bare search box over a flat list.

**Scope (frontend + one small endpoint):** hero with search, filter row (employment type,
experience level, location — the public feed endpoint already paginates; extend its query params
if needed), "latest jobs" default view, a small stats strip (open jobs / companies — one public
count endpoint), the for-companies band from 1.1, and a footer.

**Prompt:**
> Implement roadmap step 2.3 (docs/improvement-roadmap.md): marketplace redesign. Keep `/` as the
> cross-tenant feed but add: filter row (employment type, experience level, free-text location)
> wired into the public job feed endpoint (extend its query params + tests), a public stats strip
> (open job count, company count — one cached public endpoint), and a footer. Preserve
> URL-param-driven search/page state. i18n en/tr. Branch `feature/marketplace-v2`, PR into develop.

---

## Phase 3 — Notifications backbone

The module exists but only sends two emails. This phase gives candidates in-app + email visibility.
Order matters: events first, storage second, UI third, emails last.

### 3.1 Publish the missing integration events

**Scope:** `ApplicationStageChangedIntegrationEvent` (published wherever a recruiter moves an
application between stages — include old/new stage names, job title, candidate account id/email)
and `InterviewScheduledIntegrationEvent` (Interviews module — scheduled time, job title, candidate
info). Follow the existing `ApplicationSubmittedIntegrationEvent` pattern in
`Ats.Shared.Contracts`. Note: "application viewed" notifications are deliberately skipped — they
are noise and leak recruiter behaviour; a stage move to "In review" carries the same signal.

**Prompt:**
> Implement roadmap step 3.1 (docs/improvement-roadmap.md): add ApplicationStageChanged and
> InterviewScheduled integration events to Ats.Shared.Contracts, following the
> ApplicationSubmittedIntegrationEvent pattern. Publish stage-changed from the stage-move command
> in the Applications module and interview-scheduled from the schedule command in Interviews.
> Include candidate account id + email, job title, and stage/time details in the payloads.
> Integration tests assert the events are published. Branch `feature/candidate-events`, PR into
> develop.

### 3.2 In-app notification storage + API

**Scope:** Notifications module gets real Domain/Application layers: a `Notification` entity
(recipient candidate account id, type, payload/params for localization, read flag, created at),
its own schema + migration, consumers for the 3.1 events that write rows, and CandidateOnly
endpoints: list (paged), unread count, mark read / mark all read. Store i18n keys + params, not
rendered text, so the UI localizes.

**Prompt:**
> Implement roadmap step 3.2 (docs/improvement-roadmap.md): in-app notifications. In the
> Notifications module add a Notification entity (CandidateAccountId, Type, ParamsJson, IsRead,
> CreatedAtUtc), NotificationsDbContext + migration (own schema, same pattern as other modules),
> consumers persisting rows from ApplicationStageChanged and InterviewScheduled (idempotency guard
> keyed on message id), and CandidateOnly endpoints: GET list (paged), GET unread-count, POST
> mark-read / mark-all-read. Store type + params (not rendered text) so the client localizes.
> Integration tests for consumers + endpoints. Branch `feature/inapp-notifications`, PR into
> develop.

### 3.3 Notification UI (bell)

**Scope:** bell with unread badge in `PublicLayout` for candidate sessions (and on candidate
pages), dropdown with the latest notifications, mark-read on open, "view all" page, React Query
polling (60s refetch — SignalR is a later increment and must be justified before adoption).

**Prompt:**
> Implement roadmap step 3.3 (docs/improvement-roadmap.md): candidate notification bell. Add a
> NotificationBell component to PublicLayout (candidate sessions only): unread badge from the
> unread-count endpoint (React Query, 60s refetchInterval), dropdown listing latest notifications
> localized from type+params, mark-all-read when opened, link to a /candidate/notifications page
> with the paged list. i18n en/tr for every notification type. Branch
> `feature/notification-bell`, PR into develop.

### 3.4 Candidate emails for stage change & interview

**Scope:** two consumers in Notifications following the `ApplicationSubmittedConsumer` pattern
(HTML-encode inputs, idempotency guard): stage-changed (skip when the stage is internal noise —
decide the filter in review) and interview-scheduled (date/time, job, company).

**Prompt:**
> Implement roadmap step 3.4 (docs/improvement-roadmap.md): email notifications. In the
> Notifications module add ApplicationStageChangedConsumer and InterviewScheduledConsumer emailing
> the candidate, following ApplicationSubmittedConsumer exactly (HTML-encode untrusted fields,
> idempotency guard on message id, structured logs). Integration tests. Branch
> `feature/candidate-emails`, PR into develop.

---

## Phase 4 — Candidate area

### 4.1 Candidate profile page + route guard

**Scope:**
- Backend (CandidateAccounts): `GET/PUT /candidate/auth/me` profile (first/last name, phone),
  `POST change-password` (verify current password). Validation + tests.
- Frontend: `/candidate/profile` page (profile form + password form), linked from `PublicLayout`.
- Hygiene from the audit: a `RequireCandidateAuth` route wrapper so `/candidate/applications`,
  `/candidate/profile`, `/candidate/notifications` guard at the router like the company side.
- Decision point (flag in PR, don't silently choose): candidate sessions still have no refresh
  flow — either extend candidate token lifetime or add a refresh token; pick in review.

**Prompt:**
> Implement roadmap step 4.1 (docs/improvement-roadmap.md): candidate profile. CandidateAccounts
> module: extend GET me with phone, add PUT me (first/last name, phone, validated) and POST
> change-password (verifies current password; failures return typed error codes), with tests. SPA:
> /candidate/profile page with profile + change-password forms (react-hook-form + zod), link in
> PublicLayout; add a RequireCandidateAuth route wrapper and move all /candidate/* private routes
> under it. Raise the candidate-session-expiry decision in the PR description instead of deciding
> silently. i18n en/tr. Branch `feature/candidate-profile`, PR into develop.

### 4.2 My-applications detail: stage timeline + upcoming interview

**Scope:** expand the candidate applications endpoint (or add a detail endpoint) to include the
stage history and any scheduled interview (via `IInterviewDirectory`); the page gets an expandable
row or detail view with a `Timeline` (component exists) and interview info incl. meeting link
once 5.1 lands.

**Prompt:**
> Implement roadmap step 4.2 (docs/improvement-roadmap.md): candidate application detail. Add a
> CandidateOnly detail endpoint for one application returning stage history and scheduled
> interviews (cross-module read via IInterviewDirectory — extend it if needed), with tests. SPA:
> CandidateApplicationsPage rows open a detail view using the existing Timeline component, showing
> stage progress and upcoming interview date/time (+ meeting link when available). i18n en/tr.
> Branch `feature/candidate-application-detail`, PR into develop.

---

## Phase 5 — Interview experience

### 5.1 Meeting link on interviews

**Why this instead of building video:** a custom WebRTC stack is far out of MVP scope. A meeting
URL (Meet/Zoom/Jitsi) delivers the actual value — the candidate can join — with one field.

**Scope:** Interviews module: optional `MeetingUrl` on the interview (+ migration, validation:
http(s) URL); `ScheduleInterviewModal` + reschedule get the field; the URL flows into the 3.1
event payload, 3.4 email, and 4.2 candidate view.

**Prompt:**
> Implement roadmap step 5.1 (docs/improvement-roadmap.md): meeting links. Interviews module: add
> optional MeetingUrl to the interview entity (+ migration), accept it in schedule/reschedule
> commands (validate http(s)), include it in InterviewScheduledIntegrationEvent, the candidate
> email, and the candidate application detail endpoint. SPA: MeetingUrl input in
> ScheduleInterviewModal + RescheduleModal, and a join link in the candidate application detail
> and interview rows. Tests for command changes. i18n en/tr. Branch
> `feature/interview-meeting-link`, PR into develop.

### 5.2 Embedded live meeting room (stretch)

**Scope (optional, last):** a `/meet/:interviewId` page embedding Jitsi Meet via its iframe API
(public meet.jit.si for MVP; self-hosting is a later ops decision). Auto-generated room name
stored as the 5.1 MeetingUrl. Both recruiter and candidate join from their own screens.
This is the only step with an external dependency — evaluate Jitsi's terms before building.

**Prompt:**
> Implement roadmap step 5.2 (docs/improvement-roadmap.md): embedded interview room. Generate a
> Jitsi room URL when an interview is scheduled without an explicit MeetingUrl, and add a
> /meet/:interviewId page embedding it via the Jitsi iframe API (guard: interview participants
> only — company interviewer or the candidate who owns the application). Flag any Jitsi
> terms-of-service constraints found. Branch `feature/interview-live-room`, PR into develop.

---

## Suggested execution order

0.1 → 0.2 → 1.1 → 1.2 → 1.3 → 2.1 → 2.2 → 2.3 → 3.1 → 3.2 → 3.3 → 3.4 → 4.1 → 4.2 → 5.1 → 5.2

Rationale: verify what exists first; ship cheap UX corrections; build content pages that make the
marketplace feel real; then the notifications backbone (events → storage → UI → email) which
phases 4–5 consume; candidate area and interviews last because they depend on the backbone.
