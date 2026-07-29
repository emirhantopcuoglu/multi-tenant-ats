import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

/* Flat config (ESLint 9+): an ordered array of config objects, each narrowed by `files`. Later
   entries override earlier ones, so the shared bases come first and the overrides last.

   Scope note: this is a linter, not a formatter. No Prettier and no stylistic rules — the codebase
   is already consistent, and a formatting rule that fails CI on a trailing comma buys nothing while
   burying the findings that matter. Every rule enabled here catches a bug or a real hazard. */
export default tseslint.config(
  { ignores: ['dist', 'node_modules', '*.tsbuildinfo'] },

  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      /* The two hook rules are the reason this plugin is here at all. A conditional hook call or a
         missing dependency is a real runtime bug — stale closures, effects that never re-run — and
         it is invisible to the type checker. */
      ...reactHooks.configs.recommended.rules,

      /* Fast Refresh only preserves component state when a module exports components and nothing
         else. Mixing a helper into a component file silently degrades the dev loop to full reloads;
         constants are allowed because that pattern is common and harmless. */
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      /* Errors, not warnings: an unused variable after a refactor is usually a leftover, and a
         leading underscore is the escape hatch for the deliberate cases (unused route params,
         destructured rest-omit). tsc's noUnusedLocals covers the same ground but stops at the first
         project that fails to build; this reports everything in one pass. */
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],

      /* `any` disables checking silently and spreads through everything it touches. A warning
         rather than an error: the codebase has none today, and this is a ratchet to keep it that
         way, not a reason to fail a build on a deliberate escape hatch. */
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },

  {
    /* Node context, not browser: these run in the toolchain, not the app. */
    files: ['*.config.{js,ts}'],
    languageOptions: { globals: globals.node },
  },

  {
    /* Known baseline, not an exemption.

       Every file below resets its form state in `useEffect(..., [open])` when a modal opens. The
       rule is right that this is a cascading render, and the React-recommended fix is to remount on
       open with a `key` rather than to patch state afterwards — which means changing each component
       and its call sites. That is a behaviour-sensitive refactor and belongs in its own change, not
       in the one that introduces the linter.

       Listing the files explicitly rather than downgrading the rule globally is the point: a new
       component that does the same thing fails immediately, and this array only ever shrinks. When
       the last entry goes, delete the block. */
    files: [
      'src/features/applications/components/CorrectStageDialog.tsx',
      'src/features/applications/components/RejectDialog.tsx',
      'src/features/candidates/components/DeleteAccountDialog.tsx',
      'src/features/interviews/components/CancelInterviewModal.tsx',
      'src/features/interviews/components/NoShowModal.tsx',
      'src/features/interviews/components/ReassignInterviewersModal.tsx',
      'src/features/interviews/components/RescheduleModal.tsx',
      'src/features/interviews/components/ScheduleInterviewModal.tsx',
      'src/features/settings/components/InviteUserModal.tsx',
    ],
    rules: {
      'react-hooks/set-state-in-effect': 'warn',
    },
  },
);
