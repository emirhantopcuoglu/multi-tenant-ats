/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

// Vite is the dev server + build tool. Two plugins:
//   - react(): JSX/Fast Refresh for React.
//   - tailwindcss(): Tailwind v4's first-party Vite plugin (no separate PostCSS config needed).
// The "@" alias lets modules import from the src root (e.g. "@/lib/apiClient") instead of long
// relative paths; the matching TypeScript path is declared in tsconfig.app.json.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      // import.meta.dirname (Node 20.11+) is the ESM-native replacement for __dirname.
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
  },
  // Vitest reads this same config, which is the reason to prefer it over Jest here: the "@" alias
  // and the plugins above are shared, instead of being restated in a second toolchain's config and
  // left to drift.
  test: {
    // Node, not jsdom. Everything tested today is pure logic with no DOM, and a browser environment
    // is a dependency plus a per-file startup cost paid for nothing. Add it when the first component
    // test needs it, not before.
    environment: 'node',
    // No globals: tests import describe/it/expect from 'vitest' explicitly, so nothing is added to
    // the global type space and a reader can see where the helpers come from.
    globals: false,
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
  },
});
