namespace Ats.Shared.Kernel;

// Used by Jobs and CandidateAccounts. Plain strings: only input is restricted, not stored data.
// Hand-maintained because the common country/city package is GPL-3.0.
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

    // Turkey has all 81 provinces; other countries list major cities only.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CitiesByCountry =
        new Dictionary<string, IReadOnlyList<string>>
        {
            // ASCII spellings (Istanbul, not İstanbul) to avoid dotted/dotless I mismatches.
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
