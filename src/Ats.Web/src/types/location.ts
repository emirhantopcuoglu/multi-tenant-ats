/* Mirrors the backend's Jobs.Domain.SupportedCountries constant list -- a curated, hand-maintained
   set rather than a full world dataset. The obvious npm package for a cascading country/city select
   (country-state-city) is GPL-3.0, which is not safe to depend on from a private codebase, and the
   MIT-licensed alternatives only cover countries, not cities. So this list is grown deliberately by
   hand, same reasoning as CURRENCIES in enums.ts. Kept in its own file (not enums.ts) because the
   Turkey city list alone is 81 entries -- too large to sit next to the small string-union enums. */

export const COUNTRIES = [
  'Turkey',
  'United States',
  'United Kingdom',
  'Germany',
  'France',
  'Netherlands',
  'Spain',
] as const;
export type Country = (typeof COUNTRIES)[number];

/* Turkey's list is the full, official 81 provinces (a genuinely closed set). ASCII spellings
   throughout (Istanbul, not İstanbul) to match the backend list exactly -- an exact-match check
   against a Turkish-specific character (dotted/dotless I) is a classic silent-bug trap. */
export const CITIES_BY_COUNTRY: Record<Country, readonly string[]> = {
  Turkey: [
    'Adana', 'Adiyaman', 'Afyonkarahisar', 'Agri', 'Amasya', 'Ankara', 'Antalya', 'Artvin',
    'Aydin', 'Balikesir', 'Bilecik', 'Bingol', 'Bitlis', 'Bolu', 'Burdur', 'Bursa',
    'Canakkale', 'Cankiri', 'Corum', 'Denizli', 'Diyarbakir', 'Edirne', 'Elazig', 'Erzincan',
    'Erzurum', 'Eskisehir', 'Gaziantep', 'Giresun', 'Gumushane', 'Hakkari', 'Hatay', 'Isparta',
    'Mersin', 'Istanbul', 'Izmir', 'Kars', 'Kastamonu', 'Kayseri', 'Kirklareli', 'Kirsehir',
    'Kocaeli', 'Konya', 'Kutahya', 'Malatya', 'Manisa', 'Kahramanmaras', 'Mardin', 'Mugla',
    'Mus', 'Nevsehir', 'Nigde', 'Ordu', 'Rize', 'Sakarya', 'Samsun', 'Siirt', 'Sinop', 'Sivas',
    'Tekirdag', 'Tokat', 'Trabzon', 'Tunceli', 'Sanliurfa', 'Usak', 'Van', 'Yozgat',
    'Zonguldak', 'Aksaray', 'Bayburt', 'Karaman', 'Kirikkale', 'Batman', 'Sirnak', 'Bartin',
    'Ardahan', 'Igdir', 'Yalova', 'Karabuk', 'Kilis', 'Osmaniye', 'Duzce',
  ],
  'United States': [
    'New York', 'Los Angeles', 'Chicago', 'Houston', 'Phoenix', 'San Antonio', 'San Diego',
    'Dallas', 'Austin', 'San Francisco', 'Seattle', 'Denver', 'Boston', 'Miami', 'Atlanta',
    'Washington', 'Portland',
  ],
  'United Kingdom': [
    'London', 'Manchester', 'Birmingham', 'Edinburgh', 'Glasgow', 'Liverpool', 'Bristol',
    'Leeds', 'Sheffield', 'Newcastle', 'Cardiff', 'Belfast',
  ],
  Germany: [
    'Berlin', 'Munich', 'Hamburg', 'Frankfurt', 'Cologne', 'Stuttgart', 'Dusseldorf',
    'Leipzig', 'Dortmund', 'Essen',
  ],
  France: [
    'Paris', 'Marseille', 'Lyon', 'Toulouse', 'Nice', 'Nantes', 'Strasbourg', 'Montpellier',
    'Bordeaux', 'Lille',
  ],
  Netherlands: ['Amsterdam', 'Rotterdam', 'The Hague', 'Utrecht', 'Eindhoven', 'Groningen'],
  Spain: ['Madrid', 'Barcelona', 'Valencia', 'Seville', 'Zaragoza', 'Malaga', 'Bilbao'],
};
