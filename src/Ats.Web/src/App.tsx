import { ThemeToggle } from '@/components/ThemeToggle';

// Temporary token showcase (Step 1.2). It proves the design tokens flip light/dark at runtime
// and exercises the semantic color utilities. Real routing and screens replace this in later steps.
export default function App() {
  return (
    <div className="min-h-screen bg-bg text-text">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <h1 className="text-2xl font-semibold tracking-tight">Ats</h1>
        <ThemeToggle />
      </header>

      <main className="mx-auto max-w-3xl space-y-6 p-6">
        <section className="space-y-1">
          <h2 className="text-lg font-semibold">Design tokens</h2>
          <p className="text-text-muted">
            Toggle the theme — every surface, text tone, and status color flips at runtime.
          </p>
        </section>

        <div className="rounded-xl border border-border bg-card p-5 shadow-card">
          <p className="text-text">
            Card surface on <span className="text-accent">accent</span> foreground, with{' '}
            <span className="text-text-muted">muted</span> and{' '}
            <span className="text-text-disabled">disabled</span> text tones.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <span className="rounded-full bg-success-bg px-3 py-1 text-sm font-medium text-success">
            Success
          </span>
          <span className="rounded-full bg-warning-bg px-3 py-1 text-sm font-medium text-warning">
            Warning
          </span>
          <span className="rounded-full bg-danger-bg px-3 py-1 text-sm font-medium text-danger">
            Danger
          </span>
          <span className="rounded-full bg-info-bg px-3 py-1 text-sm font-medium text-info">
            Info
          </span>
        </div>

        <button
          type="button"
          className="rounded-lg bg-accent px-4 py-2 font-medium text-accent-fg transition-colors hover:bg-accent-hover"
        >
          Accent button
        </button>
      </main>
    </div>
  );
}
