/* Mirrors the backend's slug rules (Turkish letters folded to ASCII), so the preview matches what
   is stored. */
const turkishMap: Record<string, string> = {
  ı: 'i',
  ş: 's',
  ğ: 'g',
  ü: 'u',
  ö: 'o',
  ç: 'c',
};

export function slugify(value: string): string {
  return value
    .toLowerCase()
    .trim()
    .replace(/[ışğüöç]/g, (char) => turkishMap[char] ?? char)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
