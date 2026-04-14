namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class RegionResolver
    {
        private static Dictionary<short, string> _regionToContinent = new();
        private static readonly Dictionary<string, string> _regionDisplayNames = new()
        {
            // Main cities
            { "CHINA", "Jangan" },
            { "West_China", "Donwhang" },
            { "Oasis_Kingdom", "Hotan" },
            { "SD", "Abundance Grounds (Alex Desert)" },
            { "Eu", "Constantinople" },
            { "Am", "Asia Minor (Left of Samarkand)" },
            { "Ca", "Samarkand (Central Asia)" },

            // Wilderness & travel zones
            { "TQ", "Qin-Shi Tomb" },
            { "Roc", "Roc Mountain" },
            { "Thief Village", "Thief Town" },

            // Egypt region
            { "KingsValley", "King's Valley" },
            { "Pharaoh", "Holy Water Temple" },
            { "DELTA", "Alexandria" },
            { "TEMPLE", "Alexandria Job Cave (Black/Red Eggre)" },

            // Instanced & special zones
            { "GOD_TOGUI", "Togui Village" },
            { "GOD_FLAME", "Flame Mountain" },
            { "GOD_WRECK_IN", "Shipwreck (Lvl 1)" },
            { "GOD_WRECK_OUT", "Shipwreck (Lvl 2)" },
            { "EVENT_GHOST", "Ghost Event Zone" },
            { "JUPITER", "Jupiter" },
            { "PRISON", "Prison" },
            { "GM_EVENT", "GM Event Zone" },
            { "NULL", "None / Unbound" },

            // Battle arenas
            { "ARENA_OCCUPY", "Arena — Occupation" },
            { "ARENA_FLAG", "Arena — Flag" },
            { "ARENA_SCORE", "Arena — Score" },
            { "ARENA_GNGWC", "Arena — World Championship" },
            { "SIEGE_DUNGEON", "Siege Dungeon" },

            // Fortress war zones
            { "FORT_JA_AREA", "Jangan Fortress Zone" },
            { "FORT_DW_AREA", "Donwhang Fortress Zone" },
            { "FORT_HT_AREA", "Hotan Fortress Zone" },
            { "FORT_CT_AREA", "Constantinople Fortress Zone" },
            { "FORT_SK_AREA", "Samarkand Fortress Zone" },
            { "FORT_BJ_AREA", "Fort BJ Zone" },
            { "FORT_HM_AREA", "Fort HM Zone" },
            { "FORT_ER_AREA", "Fort ER Zone" },

            // Misc
            { "CHINA_SYSTEM", "Secret" }
        };

        public static async Task InitializeAsync()
        {
            var (success, regions, error) = await DBConnect.GetRegionsWithContinentsDict();

            if (!success || regions == null)
                throw new Exception($"Failed to load regions: {error}");

            _regionToContinent = regions;
        }

        public static string Resolve(short regionId)
        {
            if (!_regionToContinent.TryGetValue(regionId, out var code))
                return $"Unknown({regionId})";

            return _regionDisplayNames.TryGetValue(code, out var pretty)
                    ? pretty
                    : code.Replace("_", " ");
        }
    }
}
