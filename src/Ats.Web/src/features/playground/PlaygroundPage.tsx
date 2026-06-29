import type { ReactNode } from 'react';
import {
  Avatar,
  Badge,
  Button,
  Card,
  Checkbox,
  EmptyState,
  IconButton,
  Input,
  Select,
  Skeleton,
  StatCard,
  Textarea,
  Toggle,
} from '@/components/ui';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import {
  applicationStatusTone,
  interviewStatusTone,
  jobStatusTone,
  recommendationTone,
} from '@/lib/statusColors';
import {
  APPLICATION_STATUSES,
  INTERVIEW_STATUSES,
  FEEDBACK_RECOMMENDATIONS,
  JOB_STATUSES,
} from '@/types/enums';

/* Dev-only gallery (nominally the /playground route; real routing arrives in Step 2.1). It renders
   every primitive so we can eyeball them in light + dark. Labels are plain English on purpose —
   this page is for developers, not end users, so it isn't run through i18n. */

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="text-xs font-semibold uppercase tracking-wider text-text-muted">{title}</h2>
      {children}
    </section>
  );
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" />
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
    </svg>
  );
}

export function PlaygroundPage() {
  return (
    <div className="min-h-screen bg-bg text-text">
      <header className="sticky top-0 z-10 flex items-center justify-between border-b border-border bg-card/80 px-6 py-4 backdrop-blur">
        <h1 className="text-lg font-semibold tracking-tight">Component playground</h1>
        <div className="flex items-center gap-2">
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>

      <main className="mx-auto max-w-5xl space-y-10 p-6">
        <Section title="Buttons">
          <Card className="flex flex-wrap items-center gap-3">
            <Button variant="primary">Primary</Button>
            <Button variant="secondary">Secondary</Button>
            <Button variant="ghost">Ghost</Button>
            <Button variant="danger">Danger</Button>
            <Button variant="primary" leadingIcon={<PlusIcon />}>
              With icon
            </Button>
            <Button variant="primary" disabled>
              Disabled
            </Button>
            <IconButton aria-label="Add" icon={<PlusIcon />} />
            <IconButton aria-label="Delete" tone="danger" icon={<TrashIcon />} />
          </Card>
        </Section>

        <Section title="Form controls">
          <Card className="grid gap-4 sm:grid-cols-2">
            <Input placeholder="you@company.com" />
            <Input defaultValue="not-an-email" invalid />
            <Select defaultValue="eng">
              <option value="eng">Engineering</option>
              <option value="design">Design</option>
              <option value="ops">Operations</option>
            </Select>
            <Textarea placeholder="Add a note for the hiring team…" />
            <Checkbox label="Remote-friendly" defaultChecked />
            <Toggle label="Email me updates" defaultChecked />
          </Card>
        </Section>

        <Section title="Badges — status color mapping">
          <Card className="space-y-3">
            <div className="flex flex-wrap gap-2">
              {JOB_STATUSES.map((s) => (
                <Badge key={s} tone={jobStatusTone[s]} dot={s === 'Published'}>
                  {s}
                </Badge>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              {APPLICATION_STATUSES.map((s) => (
                <Badge key={s} tone={applicationStatusTone[s]}>
                  {s}
                </Badge>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              {INTERVIEW_STATUSES.map((s) => (
                <Badge key={s} tone={interviewStatusTone[s]}>
                  {s}
                </Badge>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              {FEEDBACK_RECOMMENDATIONS.map((s) => (
                <Badge key={s} tone={recommendationTone[s]}>
                  {s}
                </Badge>
              ))}
            </div>
          </Card>
        </Section>

        <Section title="Avatars">
          <Card className="flex items-center gap-3">
            <Avatar name="Elif Yılmaz" size="sm" />
            <Avatar name="Sarah Chen" size="md" />
            <Avatar name="Mert Demir" size="lg" />
          </Card>
        </Section>

        <Section title="Stat cards">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="Open jobs" value="12" hint="+2 this week" />
            <StatCard label="Active candidates" value="148" />
            <StatCard label="Interviews this week" value="7" />
            <StatCard label="Offer acceptance" value="86%" />
          </div>
        </Section>

        <Section title="Loading & empty states">
          <div className="grid gap-4 sm:grid-cols-2">
            <Card className="space-y-3">
              <Skeleton className="h-4 w-2/3" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-1/2" />
            </Card>
            <Card padded={false}>
              <EmptyState
                title="No applications yet"
                description="When candidates apply to this job, they’ll show up here."
                action={<Button variant="primary">Share job link</Button>}
              />
            </Card>
          </div>
        </Section>
      </main>
    </div>
  );
}
