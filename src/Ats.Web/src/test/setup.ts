import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/* Registers jest-dom's DOM matchers (toBeInTheDocument, toHaveFocus, …) and unmounts anything a
   test rendered.

   Both are normally implicit. React Testing Library auto-cleans only when Vitest's globals are on,
   and they are deliberately off — tests import their helpers explicitly — so the teardown is wired
   here instead. Without it a rendered tree survives into the next test and queries match the
   previous test's markup. */
afterEach(cleanup);
