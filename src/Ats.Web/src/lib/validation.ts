/* Shared input checks used by more than one feature. Each mirrors a backend rule; the backend
   stays authoritative — these only exist to give feedback before the round-trip. */

export function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const { protocol } = new URL(value);
    return protocol === 'http:' || protocol === 'https:';
  } catch {
    return false;
  }
}
