/* Turn free text into a URL-safe slug: lowercase, Turkish characters folded to ASCII, runs of any
   other character collapsed to single hyphens, no leading/trailing hyphens. Mirrors the backend's
   slug rules so the live preview matches what will be stored. */
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
