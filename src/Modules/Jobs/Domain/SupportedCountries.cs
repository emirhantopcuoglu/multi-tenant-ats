namespace Ats.Modules.Jobs.Domain;

// Same reasoning as SupportedCurrencies: Job.City/Job.Country stay plain strings (no enum, no
// migration) because this only tightens the input boundary, not the stored data shape. A curated
// list rather than a full world dataset -- a well-maintained, permissively-licensed country+city
// package does not exist (the common one, country-state-city, is GPL-3.0, which a private codebase
// should not depend on), so this list is hand-maintained here and grown deliberately over time.
public static class SupportedCountries
{
    public const string Turkey = "Turkey";
    public const string UnitedStates = "United States";
    public const string UnitedKingdom = "United Kingdom";
    public const string Germany = "Germany";
    public const string France = "France";
    public const string Netherlands = "Netherlands";
    public const string Spain = "Spain";

    public static readonly IReadOnlyList<string> All =
        [Turkey, UnitedStates, UnitedKingdom, Germany, France, Netherlands, Spain];

    // Turkey's list is the full, official 81 provinces (a genuinely closed set). The other
    // countries list their most common major cities rather than an exhaustive set.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CitiesByCountry =
        new Dictionary<string, IReadOnlyList<string>>
        {
            // ASCII spellings throughout (Istanbul, not İstanbul) -- matches how these names are
            // already written elsewhere in the codebase (tests, prior seed data) and avoids exact-match
            // bugs from Turkish-specific characters (dotted/dotless I is the classic trap: "İstanbul"
            // and "Istanbul" are different strings to an ordinal Contains() check).
            [Turkey] =
            [
                "Adana", "Adiyaman", "Afyonkarahisar", "Agri", "Amasya", "Ankara", "Antalya", "Artvin",
                "Aydin", "Balikesir", "Bilecik", "Bingol", "Bitlis", "Bolu", "Burdur", "Bursa",
                "Canakkale", "Cankiri", "Corum", "Denizli", "Diyarbakir", "Edirne", "Elazig", "Erzincan",
                "Erzurum", "Eskisehir", "Gaziantep", "Giresun", "Gumushane", "Hakkari", "Hatay", "Isparta",
                "Mersin", "Istanbul", "Izmir", "Kars", "Kastamonu", "Kayseri", "Kirklareli", "Kirsehir",
                "Kocaeli", "Konya", "Kutahya", "Malatya", "Manisa", "Kahramanmaras", "Mardin", "Mugla",
                "Mus", "Nevsehir", "Nigde", "Ordu", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas",
                "Tekirdag", "Tokat", "Trabzon", "Tunceli", "Sanliurfa", "Usak", "Van", "Yozgat",
                "Zonguldak", "Aksaray", "Bayburt", "Karaman", "Kirikkale", "Batman", "Sirnak", "Bartin",
                "Ardahan", "Igdir", "Yalova", "Karabuk", "Kilis", "Osmaniye", "Duzce",
            ],
            [UnitedStates] =
            [
                "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "San Antonio", "San Diego",
                "Dallas", "Austin", "San Francisco", "Seattle", "Denver", "Boston", "Miami", "Atlanta",
                "Washington", "Portland",
            ],
            [UnitedKingdom] =
            [
                "London", "Manchester", "Birmingham", "Edinburgh", "Glasgow", "Liverpool", "Bristol",
                "Leeds", "Sheffield", "Newcastle", "Cardiff", "Belfast",
            ],
            [Germany] =
            [
                "Berlin", "Munich", "Hamburg", "Frankfurt", "Cologne", "Stuttgart", "Dusseldorf",
                "Leipzig", "Dortmund", "Essen",
            ],
            [France] =
            [
                "Paris", "Marseille", "Lyon", "Toulouse", "Nice", "Nantes", "Strasbourg", "Montpellier",
                "Bordeaux", "Lille",
            ],
            [Netherlands] =
            [
                "Amsterdam", "Rotterdam", "The Hague", "Utrecht", "Eindhoven", "Groningen",
            ],
            [Spain] =
            [
                "Madrid", "Barcelona", "Valencia", "Seville", "Zaragoza", "Malaga", "Bilbao",
            ],
        };
}
