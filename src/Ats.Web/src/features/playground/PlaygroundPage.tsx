import { useState, type ReactNode } from 'react';
import {
  Avatar,
  Badge,
  Breadcrumb,
  Button,
  Card,
  Checkbox,
  Dropdown,
  EmptyState,
  IconButton,
  Input,
  KanbanCard,
  KanbanColumn,
  Modal,
  Pagination,
  Select,
  SidebarNavItem,
  Skeleton,
  SortableTH,
  StatCard,
  TabPanel,
  Table,
  Tabs,
  TBody,
  TD,
  TH,
  THead,
  TR,
  TableFooter,
  Textarea,
  Timeline,
  TimelineItem,
  Toggle,
  Tooltip,
  useToast,
  type SortDirection,
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

function DotsIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
      <circle cx="5" cy="12" r="1.6" />
      <circle cx="12" cy="12" r="1.6" />
      <circle cx="19" cy="12" r="1.6" />
    </svg>
  );
}

function ToastDemoButton() {
  const { toast } = useToast();
  return (
    <Button
      variant="secondary"
      onClick={() =>
        toast({ title: 'Interview scheduled', description: 'Elif Yılmaz · Technical · Mon 10:00', tone: 'success' })
      }
    >
      Show toast
    </Button>
  );
}

const candidateRows = [
  { name: 'Elif Yılmaz', role: 'Sr. Frontend Engineer', stage: 'Interview', status: 'Active' as const, applied: '2d ago' },
  { name: 'Sarah Chen', role: 'Backend Engineer', stage: 'Offer', status: 'Hired' as const, applied: '5d ago' },
  { name: 'Ahmet Kaya', role: 'DevOps Engineer', stage: 'Applied', status: 'Rejected' as const, applied: '1w ago' },
];

export function PlaygroundPage() {
  const [modalOpen, setModalOpen] = useState(false);
  const [tab, setTab] = useState('overview');
  const [sortDir, setSortDir] = useState<SortDirection>('asc');
  const [page, setPage] = useState(2);

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

        <Section title="Overlays — modal, dropdown, tooltip, toast">
          <Card className="flex flex-wrap items-center gap-3">
            <Button onClick={() => setModalOpen(true)}>Open dialog</Button>
            <Dropdown
              align="start"
              trigger={<Button variant="secondary" leadingIcon={<DotsIcon />}>Actions</Button>}
              items={[
                { key: 'view', label: 'View profile', onSelect: () => undefined },
                { key: 'edit', label: 'Edit application', onSelect: () => undefined },
                { key: 'reject', label: 'Move to rejected', onSelect: () => undefined, tone: 'danger', separatorBefore: true },
              ]}
            />
            <Tooltip content="Tooltips give extra context">
              <Button variant="ghost">Hover me</Button>
            </Tooltip>
            <ToastDemoButton />
          </Card>
        </Section>

        <Section title="Tabs">
          <Card>
            <Tabs
              value={tab}
              onValueChange={setTab}
              items={[
                { value: 'overview', label: 'Overview' },
                { value: 'details', label: 'Details' },
                { value: 'activity', label: 'Activity' },
              ]}
            >
              <TabPanel value="overview">
                <p className="text-sm text-text-muted">A quick summary of the role and pipeline health.</p>
              </TabPanel>
              <TabPanel value="details">
                <p className="text-sm text-text-muted">Full job description, requirements and salary band.</p>
              </TabPanel>
              <TabPanel value="activity">
                <p className="text-sm text-text-muted">Every stage change, note and email, in order.</p>
              </TabPanel>
            </Tabs>
          </Card>
        </Section>

        <Section title="Table — sortable header, row actions, pagination footer">
          <Table>
            <THead>
              <TR>
                <SortableTH direction={sortDir} onSort={() => setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'))}>
                  Candidate
                </SortableTH>
                <TH>Role</TH>
                <TH>Stage</TH>
                <TH>Status</TH>
                <TH className="text-right">Applied</TH>
                <TH />
              </TR>
            </THead>
            <TBody>
              {candidateRows.map((row) => (
                <TR key={row.name} interactive>
                  <TD>
                    <div className="flex items-center gap-2.5">
                      <Avatar name={row.name} size="sm" />
                      <span className="font-medium">{row.name}</span>
                    </div>
                  </TD>
                  <TD className="text-text-muted">{row.role}</TD>
                  <TD>{row.stage}</TD>
                  <TD>
                    <Badge tone={applicationStatusTone[row.status]}>{row.status}</Badge>
                  </TD>
                  <TD className="text-right text-text-muted">{row.applied}</TD>
                  <TD className="text-right">
                    <Dropdown
                      trigger={<IconButton aria-label="Row actions" icon={<DotsIcon />} className="border-transparent bg-transparent" />}
                      items={[
                        { key: 'view', label: 'View profile', onSelect: () => undefined },
                        { key: 'reject', label: 'Move to rejected', onSelect: () => undefined, tone: 'danger', separatorBefore: true },
                      ]}
                    />
                  </TD>
                </TR>
              ))}
            </TBody>
            <tfoot>
              <tr>
                <td colSpan={6} className="p-0">
                  <TableFooter>
                    <span className="text-sm text-text-muted">Showing 6 of 148 applications</span>
                    <Pagination page={page} pageCount={25} onPageChange={setPage} />
                  </TableFooter>
                </td>
              </tr>
            </tfoot>
          </Table>
        </Section>

        <Section title="Breadcrumb">
          <Card>
            <Breadcrumb
              items={[{ label: 'Jobs', href: '/jobs' }, { label: 'Senior Frontend Engineer' }]}
            />
          </Card>
        </Section>

        <Section title="Pipeline — Kanban">
          <div className="flex gap-3 overflow-x-auto pb-2">
            <KanbanColumn title="Applied" count={2}>
              <KanbanCard>
                <div className="flex items-center gap-2">
                  <Avatar name="Ahmet Kaya" size="sm" />
                  <span className="text-sm font-medium">Ahmet Kaya</span>
                </div>
                <span className="text-xs text-text-muted">DevOps Engineer</span>
              </KanbanCard>
            </KanbanColumn>
            <KanbanColumn title="Interview" count={1}>
              <KanbanCard>
                <div className="flex items-center gap-2">
                  <Avatar name="Elif Yılmaz" size="sm" />
                  <span className="text-sm font-medium">Elif Yılmaz</span>
                </div>
                <span className="text-xs text-text-muted">Sr. Frontend Engineer</span>
              </KanbanCard>
            </KanbanColumn>
          </div>
        </Section>

        <Section title="Activity timeline">
          <Card>
            <Timeline>
              <TimelineItem tone="accent" title={<><strong className="font-semibold">Elif Yılmaz</strong> submitted an application.</>} meta="2 days ago" />
              <TimelineItem tone="success" title={<><strong className="font-semibold">You</strong> moved the candidate to Interview.</>} meta="1 day ago" />
              <TimelineItem tone="danger" last title={<><strong className="font-semibold">You</strong> rejected the application.</>} meta="3 hours ago" />
            </Timeline>
          </Card>
        </Section>

        <Section title="Sidebar navigation">
          <Card className="max-w-xs space-y-1">
            <SidebarNavItem label="Overview" active />
            <SidebarNavItem label="Jobs" />
            <SidebarNavItem label="Applications" />
            <SidebarNavItem label="Interviews" />
          </Card>
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

      <Modal
        open={modalOpen}
        onOpenChange={setModalOpen}
        title="Schedule interview"
        description="Set up an interview for Elif Yılmaz"
        footer={
          <>
            <Button variant="secondary" onClick={() => setModalOpen(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={() => setModalOpen(false)}>
              Schedule
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <Select defaultValue="technical">
            <option value="phone">Phone screen</option>
            <option value="technical">Technical</option>
            <option value="final">Final</option>
          </Select>
          <Input type="datetime-local" />
        </div>
      </Modal>
    </div>
  );
}
