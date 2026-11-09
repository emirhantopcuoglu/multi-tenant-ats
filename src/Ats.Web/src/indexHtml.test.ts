import { describe, expect, it } from 'vitest';
import indexHtml from '../index.html?raw';

// The CSP in nginx.conf.template runs only scripts loaded from our origin, so an inline script in
// the shell would silently not run in production.
describe('index.html', () => {
  it('loads every script from a file', () => {
    const scriptTags = indexHtml.match(/<script\b[^>]*>/g) ?? [];

    expect(scriptTags.length).toBeGreaterThan(0);
    expect(scriptTags.filter((tag) => !/\ssrc=/.test(tag))).toEqual([]);
  });
});
