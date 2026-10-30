import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/* Registers jest-dom matchers and unmounts after each test. RTL only does the cleanup itself when
   Vitest globals are on, and they are off here. */
afterEach(cleanup);
