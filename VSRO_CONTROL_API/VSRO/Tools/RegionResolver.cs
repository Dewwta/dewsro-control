using VSRO_CONTROL_API.VSRO.DTO.Regions;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class RegionResolver
    {


        private static Dictionary<short, (string Continent, string AreaName)> _regionToContinent = new();
        private static readonly Dictionary<string, string> _regionDisplayNames = new()
        {
            // Main cities
            { "CHINA", "China" },
            { "West_China", "Donwhang" },
            { "Oasis_Kingdom", "Hotan" },
            { "SD", "Abundance Grounds (Alex Desert)" },
            { "Eu", "Constantinople" },
            { "Am", "Samarakand (Central Asia)" },
            { "Ca", "Samarakand (Central Asia)" },

            // Wilderness & travel zones
            { "TQ", "Qin-Shi Tomb" },
            { "Roc", "Roc Mountain" },
            { "Thief Village", "Thief Town" },

            // Egypt region
            { "KingsValley", "Kings Valley" },
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
        private sealed class DungeonRoomBounds
        {
            public required string RoomName { get; init; }

            public required int MinXSec { get; init; }
            public required int MaxXSec { get; init; }

            public required int MinYSec { get; init; }
            public required int MaxYSec { get; init; }
        }

        private static readonly List<DungeonRoomBounds> _qinShiB2Rooms = new()
        {
            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:1",
                MinXSec = 120,
                MaxXSec = 122,
                MinYSec = 129,
                MaxYSec = 131
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:2",
                MinXSec = 123,
                MaxXSec = 126,
                MinYSec = 129,
                MaxYSec = 132
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:3",
                MinXSec = 126,
                MaxXSec = 128,
                MinYSec = 129,
                MaxYSec = 131
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:4",
                MinXSec = 130,
                MaxXSec = 132,
                MinYSec = 129,
                MaxYSec = 131
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:5",
                MinXSec = 133,
                MaxXSec = 135,
                MinYSec = 129,
                MaxYSec = 131
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:6",
                MinXSec = 136,
                MaxXSec = 138,
                MinYSec = 129,
                MaxYSec = 131
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:7",
                MinXSec = 117,
                MaxXSec = 119,
                MinYSec = 127,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:8",
                MinXSec = 120,
                MaxXSec = 122,
                MinYSec = 127,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:9",
                MinXSec = 123,
                MaxXSec = 125,
                MinYSec = 127,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:10",
                MinXSec = 126,
                MaxXSec = 129,
                MinYSec = 126,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:11",
                MinXSec = 130,
                MaxXSec = 132,
                MinYSec = 127,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:12",
                MinXSec = 133,
                MaxXSec = 135,
                MinYSec = 127,
                MaxYSec = 129
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:13",
                MinXSec = 136,
                MaxXSec = 138,
                MinYSec = 127,
                MaxYSec = 128
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:14",
                MinXSec = 117,
                MaxXSec = 119,
                MinYSec = 124,
                MaxYSec = 125
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:15",
                MinXSec = 120,
                MaxXSec = 123,
                MinYSec = 123,
                MaxYSec = 126
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:16",
                MinXSec = 123,
                MaxXSec = 125,
                MinYSec = 124,
                MaxYSec = 125
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:17",
                MinXSec = 127,
                MaxXSec = 128,
                MinYSec = 124,
                MaxYSec = 125
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:18",
                MinXSec = 130,
                MaxXSec = 132,
                MinYSec = 124,
                MaxYSec = 125
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:19",
                MinXSec = 133,
                MaxXSec = 135,
                MinYSec = 123,
                MaxYSec = 126
            },

            new()
            {
                RoomName = "Qin-Shi Tomb|floor:2|room:20",
                MinXSec = 136,
                MaxXSec = 138,
                MinYSec = 124,
                MaxYSec = 126
            },


        };

        private static readonly List<ReadableRegionEntry> _readableRegions = new()
        {
            // CH - Hunting at {name} in {parent}
            new() { ParentRegionName = "China", Name = "Tomb",                              MinX = 170, MaxX = 174, MinY = 92, MaxY = 94 },
            new() { ParentRegionName = "China", Name = "Qin-Shi Entrance",                  MinX = 171, MaxX = 174, MinY = 99, MaxY = 103 },
            new() { ParentRegionName = "China", Name = "East Jangan Grassland",             MinX = 170, MaxX = 174, MinY = 97, MaxY = 98 },
            new() { ParentRegionName = "China", Name = "South-East Jangan Grassland",       MinX = 170, MaxX = 174, MinY = 95, MaxY = 96 },
            new() { ParentRegionName = "China", Name = "Jangan",                            MinX = 167, MaxX = 169, MinY = 96, MaxY = 98 },
            new() { ParentRegionName = "China", Name = "Water Ghost Marshland",             MinX = 166, MaxX = 170, MinY = 99, MaxY = 101 },
            new() { ParentRegionName = "China", Name = "West Jangan Grassland",             MinX = 166, MaxX = 166, MinY = 97, MaxY = 98 },
            new() { ParentRegionName = "China", Name = "South-West Jangan Grassland",       MinX = 165, MaxX = 166, MinY = 91, MaxY = 96 },
            new() { ParentRegionName = "China", Name = "South Jangan Grassland",            MinX = 167, MaxX = 169, MinY = 91, MaxY = 96 },
            new() { ParentRegionName = "China", Name = "Exorcist's Home",                   MinX = 162, MaxX = 165, MinY = 97, MaxY = 99 },
            new() { ParentRegionName = "China", Name = "Yeoha Forest",                      MinX = 161, MaxX = 164, MinY = 95, MaxY = 96 },
            new() { ParentRegionName = "China", Name = "Tiger Mountain",                    MinX = 161, MaxX = 164, MinY = 94, MaxY = 94 },
            new() { ParentRegionName = "China", Name = "Bandit Mountain Stronghold",        MinX = 156, MaxX = 164, MinY = 89, MaxY = 93 },
            new() { ParentRegionName = "China", Name = "China West Ferry",                  MinX = 156, MaxX = 158, MinY = 96, MaxY = 97 },
            new() { ParentRegionName = "China", Name = "China East Ferry",                  MinX = 159, MaxX = 161, MinY = 97, MaxY = 98 },
            new() { ParentRegionName = "China", Name = "Northwest Tiger Den",               MinX = 156, MaxX = 160, MinY = 94, MaxY = 95 },
            new() { ParentRegionName = "China", Name = "Road To East Ferry",                MinX = 159, MaxX = 161, MinY = 96, MaxY = 96 },

            // WC 
            new() { ParentRegionName = "Western China", Name = "Donwhang",             MinX = 151, MaxX = 153, MinY = 102, MaxY = 104 },
            new() { ParentRegionName = "Western China", Name = "Grassland Road",            MinX = 151, MaxX = 161, MinY = 105, MaxY = 106 },
            new() { ParentRegionName = "Western China", Name = "Hyungo Homeland",           MinX = 154, MaxX = 161, MinY = 102, MaxY = 104 },
            new() { ParentRegionName = "Western China", Name = "North Earth Ghost Canyon",  MinX = 151, MaxX = 155, MinY = 99,  MaxY = 101 },
            new() { ParentRegionName = "Western China", Name = "South Earth Ghost Canyon",  MinX = 151, MaxX = 155, MinY = 96,  MaxY = 98 },
            new() { ParentRegionName = "Western China", Name = "Western China East Ferry",  MinX = 159, MaxX = 161, MinY = 97,  MaxY = 101 },
            new() { ParentRegionName = "Western China", Name = "Western China West Ferry",  MinX = 156, MaxX = 158, MinY = 99,  MaxY = 101 },
            new() { ParentRegionName = "Western China", Name = "Okmungwan West Oasis",      MinX = 148, MaxX = 150, MinY = 100, MaxY = 106 },
            new() { ParentRegionName = "Western China", Name = "Stone Cave Entrance",       MinX = 147, MaxX = 147, MinY = 106, MaxY = 106 },
            new() { ParentRegionName = "Western China", Name = "North Tarim Basin",         MinX = 142, MaxX = 150, MinY = 96,  MaxY = 99 },
            new() { ParentRegionName = "Western China", Name = "Oasis",                     MinX = 151, MaxX = 153, MinY = 89,  MaxY = 95 },
            new() { ParentRegionName = "Western China", Name = "Black Robber Den",          MinX = 146, MaxX = 150, MinY = 91,  MaxY = 93 }, 
            new() { ParentRegionName = "Western China", Name = "South of Black Robber Den", MinX = 146, MaxX = 150, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Western China", Name = "Central Tarim Basin",       MinX = 142, MaxX = 150, MinY = 94,  MaxY = 95 },
            new() { ParentRegionName = "Western China", Name = "Tarim North Ferrie",        MinX = 142, MaxX = 145, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Western China", Name = "Tarim South Ferrie",        MinX = 142, MaxX = 145, MinY = 91,  MaxY = 93 },
            
            // EU 
            new() { ParentRegionName = "Eastern Europe", Name = "Constantinople",           MinX = 77, MaxX = 81, MinY = 103, MaxY = 107 },
            new() { ParentRegionName = "Eastern Europe", Name = "Witches Lighthouse",       MinX = 80, MaxX = 81, MinY = 108, MaxY = 110 },
            new() { ParentRegionName = "Eastern Europe", Name = "Traveler's Hill",          MinX = 77, MaxX = 79, MinY = 108, MaxY = 110 },
            new() { ParentRegionName = "Eastern Europe", Name = "Bloody Hill",              MinX = 74, MaxX = 76, MinY = 107, MaxY = 110 },
            new() { ParentRegionName = "Eastern Europe", Name = "Golden Plain",             MinX = 74, MaxX = 76, MinY = 104, MaxY = 106 },
            new() { ParentRegionName = "Eastern Europe", Name = "North Forest Of Sorrow",   MinX = 68, MaxX = 73, MinY = 109, MaxY = 110 },
            new() { ParentRegionName = "Eastern Europe", Name = "South Forest Of Sorrow",   MinX = 68, MaxX = 73, MinY = 104, MaxY = 108 },
            new() { ParentRegionName = "Eastern Europe", Name = "Desperado Hill",           MinX = 68, MaxX = 73, MinY = 101, MaxY = 103 },
            new() { ParentRegionName = "Eastern Europe", Name = "Garden Of Gods",           MinX = 68, MaxX = 73, MinY = 97,  MaxY = 100 },
            new() { ParentRegionName = "Eastern Europe", Name = "Forest Of Dusk",           MinX = 74, MaxX = 77, MinY = 99,  MaxY = 103 },
            new() { ParentRegionName = "Eastern Europe", Name = "Shore Of Dawn",            MinX = 74, MaxX = 77, MinY = 97,  MaxY = 98 },
            new() { ParentRegionName = "Eastern Europe", Name = "Jupiter Temple",           MinX = 68, MaxX = 71, MinY = 95,  MaxY = 96 },
            
            // ASIA MINOR
            new() { ParentRegionName = "Asia Minor", Name = "North Beach",                  MinX = 90, MaxX = 91, MinY = 101,  MaxY = 106 },
            new() { ParentRegionName = "Asia Minor", Name = "South Beach",                  MinX = 90, MaxX = 91, MinY = 97,   MaxY = 100 },
            new() { ParentRegionName = "Asia Minor", Name = "Droa Dock",                    MinX = 88, MaxX = 89, MinY = 101,  MaxY = 106 },
            new() { ParentRegionName = "Asia Minor", Name = "Sigia Dock",                   MinX = 88, MaxX = 89, MinY = 97,   MaxY = 100 },
            new() { ParentRegionName = "Asia Minor", Name = "Pond Ruins",                   MinX = 92, MaxX = 93, MinY = 97,   MaxY = 101 },
            new() { ParentRegionName = "Asia Minor", Name = "Ararat Mountain",              MinX = 92, MaxX = 97, MinY = 102,  MaxY = 107 },
            new() { ParentRegionName = "Asia Minor", Name = "Evil Order Fortress",          MinX = 94, MaxX = 97, MinY = 97,   MaxY = 101 },
            new() { ParentRegionName = "Asia Minor", Name = "Anatolian Plateau",            MinX = 98, MaxX = 104, MinY = 100, MaxY = 106 },
            new() { ParentRegionName = "Asia Minor", Name = "Haran's Tower",                MinX = 98, MaxX = 101, MinY = 96,  MaxY = 99 },
            new() { ParentRegionName = "Asia Minor", Name = "Roc Mtn. Aircraft Dock",       MinX = 102, MaxX = 103, MinY = 96, MaxY = 99 },
            
            // CENTRAL ASIA
            new() { ParentRegionName = "Central Asia", Name = "Ong Habitat",                MinX = 105, MaxX = 108, MinY = 101, MaxY = 105 },
            new() { ParentRegionName = "Central Asia", Name = "Samarakand",                 MinX = 107, MaxX = 108, MinY = 106, MaxY = 107 },
            new() { ParentRegionName = "Central Asia", Name = "West Samarakand Fields",     MinX = 104, MaxX = 106, MinY = 106, MaxY = 108 },
            new() { ParentRegionName = "Central Asia", Name = "North Samarakand Fields",    MinX = 107, MaxX = 108, MinY = 108, MaxY = 108 },
            new() { ParentRegionName = "Central Asia", Name = "South West Samarakand Fields",MinX = 105, MaxX = 108, MinY = 106, MaxY = 106 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (NW)",         MinX = 109, MaxX = 111, MinY = 106, MaxY = 108 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (N)",          MinX = 112, MaxX = 113, MinY = 106, MaxY = 108 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (NE)",         MinX = 114, MaxX = 116, MinY = 106, MaxY = 108 }, 
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (W)",          MinX = 109, MaxX = 111, MinY = 103, MaxY = 105 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (C)",          MinX = 112, MaxX = 113, MinY = 103, MaxY = 105 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (E)",          MinX = 114, MaxX = 116, MinY = 103, MaxY = 105 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (SW)",         MinX = 109, MaxX = 111, MinY = 101, MaxY = 102 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (S)",          MinX = 112, MaxX = 113, MinY = 101, MaxY = 102 },
            new() { ParentRegionName = "Central Asia", Name = "Huns Garrison (SE)",         MinX = 114, MaxX = 116, MinY = 101, MaxY = 102 },
            new() { ParentRegionName = "Central Asia", Name = "Pamir Plateau",              MinX = 117, MaxX = 120, MinY = 102,  MaxY = 108 },
            new() { ParentRegionName = "Central Asia", Name = "Roc Mtn. Aircraft Dock",     MinX = 117, MaxX = 120, MinY = 101,  MaxY = 101 },

            // HOTAN
            new() { ParentRegionName = "Hotan Kingdom", Name = "Hotan Palace",              MinX = 134, MaxX = 136, MinY = 91,  MaxY = 94 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Hotan Fields (E)",          MinX = 133, MaxX = 133, MinY = 89,  MaxY = 94 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Karakoram Trail Entrance (S)",MinX = 131, MaxX = 132, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Karakoram Main Entrance",   MinX = 131, MaxX = 132, MinY = 91,  MaxY = 93 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Karakoram Trail Entrance (N)",MinX = 131, MaxX = 132, MinY = 94,  MaxY = 96 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Hotan Field (S)",           MinX = 134, MaxX = 137, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Black Jade River",          MinX = 138, MaxX = 139, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Ferry South",               MinX = 140, MaxX = 141, MinY = 89,  MaxY = 90 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Ferry North",               MinX = 140, MaxX = 141, MinY = 91,  MaxY = 92 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Pao Village",               MinX = 137, MaxX = 139, MinY = 91,  MaxY = 92 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Grassland Road (E)",        MinX = 137, MaxX = 140, MinY = 93,  MaxY = 95 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Hotan Field (NE)",          MinX = 137, MaxX = 140, MinY = 96,  MaxY = 97 },
            new() { ParentRegionName = "Hotan Kingdom", Name = "Hotan Field (N)",           MinX = 133, MaxX = 136, MinY = 95,  MaxY = 97 },
            
            // Taklamakan
            new() { ParentRegionName = "Taklamakan", Name = "Mysterious Death Desert (E)",  MinX = 132, MaxX = 139, MinY = 98,  MaxY = 100 },
            new() { ParentRegionName = "Taklamakan", Name = "Mysterious Death Desert (W)",  MinX = 124, MaxX = 131, MinY = 98,  MaxY = 100 },
            new() { ParentRegionName = "Taklamakan", Name = "Anahita River (E)",            MinX = 132, MaxX = 139, MinY = 101, MaxY = 102 },
            new() { ParentRegionName = "Taklamakan", Name = "Anahita River (W)",            MinX = 126, MaxX = 131, MinY = 101, MaxY = 102 },
            new() { ParentRegionName = "Taklamakan", Name = "Taklamakan Desert (N)",        MinX = 130, MaxX = 139, MinY = 103, MaxY = 106 },
            new() { ParentRegionName = "Taklamakan", Name = "Niya Remains",                 MinX = 125, MaxX = 129, MinY = 103, MaxY = 106 },
            new() { ParentRegionName = "Taklamakan", Name = "Abyss Tunnel Entrance",        MinX = 125, MaxX = 125, MinY = 102, MaxY = 102 },

            // CONNECTING CAVE
            new() { ParentRegionName = "Taklamakan", Name = "Dark Cave (S)",                MinX = 126, MaxX = 127, MinY = 96,  MaxY = 97 },

            // KARAKORAM
            new() { ParentRegionName = "Karakoram", Name = "Ancient Remains (NW)",          MinX = 125, MaxX = 127, MinY = 90, MaxY = 92 },
            new() { ParentRegionName = "Karakoram", Name = "Ancient Remains (NE)",          MinX = 128, MaxX = 129, MinY = 90, MaxY = 92 },
            new() { ParentRegionName = "Karakoram", Name = "Ancient Remains (SW)",          MinX = 125, MaxX = 127, MinY = 87, MaxY = 89 },
            new() { ParentRegionName = "Karakoram", Name = "Ancient Remains (SE)",          MinX = 128, MaxX = 129, MinY = 87, MaxY = 89 },
            new() { ParentRegionName = "Karakoram", Name = "Spider Forest (N)",             MinX = 122, MaxX = 130, MinY = 93, MaxY = 95 },
            new() { ParentRegionName = "Karakoram", Name = "Spider Forest (W)",             MinX = 122, MaxX = 124, MinY = 90, MaxY = 92 },
            new() { ParentRegionName = "Karakoram", Name = "Oasis Forest",                  MinX = 122, MaxX = 124, MinY = 84, MaxY = 89 },
            new() { ParentRegionName = "Karakoram", Name = "Korakoram Forest (S)",          MinX = 125, MaxX = 130, MinY = 84, MaxY = 89 },
            new() { ParentRegionName = "Karakoram", Name = "Korakoram Forest (E)",          MinX = 125, MaxX = 130, MinY = 90, MaxY = 92 },
            new() { ParentRegionName = "Karakoram", Name = "Aircraft Dock (NW)",            MinX = 120, MaxX = 121, MinY = 92, MaxY = 95 },
            new() { ParentRegionName = "Karakoram", Name = "Aircraft Dock (SW)",            MinX = 120, MaxX = 121, MinY = 85, MaxY = 86 },

            // ROC MOUNTAIN PRESENCE
            new() { ParentRegionName = "Roc Mountain", Name = "Aircraft Dock (NE)",         MinX = 118, MaxX = 118, MinY = 94, MaxY = 95 },
            new() { ParentRegionName = "Roc Mountain", Name = "Aircraft Dock (SE)",         MinX = 118, MaxX = 118, MinY = 86, MaxY = 87 },
            new() { ParentRegionName = "Roc Mountain", Name = "Roc Mountain Forest (N)",    MinX = 109, MaxX = 114, MinY = 94, MaxY = 98 },
            new() { ParentRegionName = "Roc Mountain", Name = "Brain Peak",                 MinX = 115, MaxX = 117, MinY = 93, MaxY = 98 },
            new() { ParentRegionName = "Roc Mountain", Name = "Eye Peak",                   MinX = 105, MaxX = 108, MinY = 94, MaxY = 98 },
            new() { ParentRegionName = "Roc Mountain", Name = "Lost Town",                  MinX = 104, MaxX = 108, MinY = 92, MaxY = 93 },
            new() { ParentRegionName = "Roc Mountain", Name = "Forest Roads (W)",           MinX = 104, MaxX = 108, MinY = 88, MaxY = 91 },
            new() { ParentRegionName = "Roc Mountain", Name = "Tail Peak",                  MinX = 104, MaxX = 108, MinY = 85, MaxY = 87 },
            new() { ParentRegionName = "Roc Mountain", Name = "Shepherd Town",              MinX = 109, MaxX = 109, MinY = 85, MaxY = 86 },
            new() { ParentRegionName = "Roc Mountain", Name = "Roc Mountain Forest (S)",    MinX = 110, MaxX = 114, MinY = 83, MaxY = 87 },
            new() { ParentRegionName = "Roc Mountain", Name = "Beak Peak",                  MinX = 115, MaxX = 117, MinY = 83, MaxY = 87 },
            new() { ParentRegionName = "Roc Mountain", Name = "Wind Town Lake (E)",         MinX = 117, MaxX = 118, MinY = 88, MaxY = 92 },
            new() { ParentRegionName = "Roc Mountain", Name = "Wind Town",                  MinX = 115, MaxX = 116, MinY = 89, MaxY = 90 },
            new() { ParentRegionName = "Roc Mountain", Name = "Heart Peak",                 MinX = 110, MaxX = 113, MinY = 93, MaxY = 93 },
            new() { ParentRegionName = "Roc Mountain", Name = "Gate Of Ruler",              MinX = 110, MaxX = 113, MinY = 91, MaxY = 92 },
            new() { ParentRegionName = "Roc Mountain", Name = "Roc Mountain Forest (W)",    MinX = 109, MaxX = 109, MinY = 90, MaxY = 92 },
            new() { ParentRegionName = "Roc Mountain", Name = "Claw Peak",                  MinX = 109, MaxX = 110, MinY = 87, MaxY = 89 },
            new() { ParentRegionName = "Roc Mountain", Name = "Wing Peak",                  MinX = 113, MaxX = 114, MinY = 89, MaxY = 92 },
            
            // STORM AND CLOUD DESERT
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Red Hamada",   MinX = 65, MaxX = 68, MinY = 87,  MaxY = 88 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Red Leg",      MinX = 69, MaxX = 72, MinY = 87,  MaxY = 88 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Red Eggre",    MinX = 73, MaxX = 76, MinY = 87,  MaxY = 88 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Mushroom Rock",MinX = 65, MaxX = 68, MinY = 84,  MaxY = 86 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Salt Post",    MinX = 69, MaxX = 72, MinY = 84,  MaxY = 86 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Salt Desert",  MinX = 73, MaxX = 76, MinY = 84,  MaxY = 86 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Black Hamada", MinX = 65, MaxX = 68, MinY = 81,  MaxY = 83 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Black Leg",    MinX = 69, MaxX = 72, MinY = 81,  MaxY = 83 },
            new() { ParentRegionName = "The Storm And Cloud Desert", Name = "Black Eggre",  MinX = 73, MaxX = 76, MinY = 81,  MaxY = 83 },

            // KINGS VALLEY
            new() { ParentRegionName = "Kings Valley", Name = "North Echo Valley",          MinX = 56, MaxX = 63, MinY = 75,  MaxY = 77 },
            new() { ParentRegionName = "Kings Valley", Name = "South Echo Valley",          MinX = 56, MaxX = 63, MinY = 72,  MaxY = 73 },
            new() { ParentRegionName = "Kings Valley", Name = "Red Ground",                 MinX = 64, MaxX = 66, MinY = 72,  MaxY = 77 },
            new() { ParentRegionName = "Kings Valley", Name = "Chaos Maze",                 MinX = 67, MaxX = 71, MinY = 72,  MaxY = 77 },
            new() { ParentRegionName = "Kings Valley", Name = "The Holy Water Temple Entrance",MinX = 72, MaxX = 75, MinY = 73,  MaxY = 77 },

            // ALEXANDRIA
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Alexandria City (S)",  MinX = 47, MaxX = 49, MinY = 88,  MaxY = 95 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Alexandria City (N)",  MinX = 50, MaxX = 51, MinY = 91,  MaxY = 92 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "South Egypt Grassland",MinX = 50, MaxX = 51, MinY = 88,  MaxY = 90 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Mahamura Beach",       MinX = 52, MaxX = 56, MinY = 92,  MaxY = 93 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Abundance Ground",     MinX = 52, MaxX = 55, MinY = 90,  MaxY = 91 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Abu Mena Ruin",        MinX = 52, MaxX = 54, MinY = 88,  MaxY = 89 },
            new() { ParentRegionName = "Egypt (Alexandria)", Name = "Forbidden Plains",     MinX = 52, MaxX = 53, MinY = 85,  MaxY = 87 },

            // DUNGEON PRESENCE

            //new() { ParentRegionName = "China", Name = "West Jangan Grassland",         MinX = 170, MaxX = 174, MinY = 95, MaxY = 96 },
        };

        public static async Task InitializeAsync()
        {
            var (success, regions, error) = await DBConnect.GetRegionsWithContinentsDict();

            if (!success || regions == null)
                throw new Exception($"Failed to load regions: {error}");

            _regionToContinent = regions;
        }

        public static string Resolve(short regionId, int secX = 0, int secY = 0)
        {
            // This returns the full walkable region as a string, used to determine teleport and bot behavior.
            // Readable is only for detecting town and UI text.
            // Qin Shi
            if (regionId >= -32766 && regionId <= -32761)
            {
                // -32761 = floor 1 (B1), -32762 = floor 2 (B2), etc.
                int floor = Math.Abs(regionId) - 32760; // 1-6

                // Only attempt room detection on B2 (regionId == -32762)
                if (regionId == -32762 && (secX != 0 || secY != 0))
                {
                    var match = _qinShiB2Rooms.FirstOrDefault(r =>
                        secX >= r.MinXSec && secX <= r.MaxXSec &&
                        secY >= r.MinYSec && secY <= r.MaxYSec);

                    if (match != null)
                        return match.RoomName;
                }

                return $"Qin-Shi Tomb|floor:{floor}";
            }

            // Stone Cave — Z floor detection
            // -32767 and -32759 are both Stone Cave instances
            if (regionId == -32767 || regionId == -32759)
                return "Stone Cave";

            if (!_regionToContinent.TryGetValue(regionId, out var code))
                return $"Unknown({regionId})";

            return _regionDisplayNames.TryGetValue(code.Continent, out var pretty)
                ? pretty
                : code.Continent.Replace("_", " ");
        }
        public static string ResolveReadable(int xSec, int ySec, short fallbackRegionId)
        {

            // Check sub-regions first
            var match = _readableRegions.FirstOrDefault(r =>
                xSec >= r.MinX && xSec <= r.MaxX &&
                ySec >= r.MinY && ySec <= r.MaxY);

            if (match != null)
                return $"{match.Name}, {match.ParentRegionName}";

            return Resolve(fallbackRegionId, xSec, ySec);
        }
    }
}
