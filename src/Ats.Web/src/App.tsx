// Placeholder root screen for the scaffold (Step 1.1). It only proves the toolchain works:
// React renders, the "@" alias resolves, and Tailwind utility classes apply (including the
// dark-mode variant). Real routing, theming, and screens arrive in the following steps.
export default function App() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-white text-slate-900 dark:bg-slate-950 dark:text-slate-100">
      <h1 className="text-4xl font-semibold tracking-tight">Ats</h1>
    </div>
  );
}
