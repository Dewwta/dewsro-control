using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.Enums;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    public class TeleportGraph
    {
        #region - Constants -

        const uint DEFAULT_PAD = 0,
                   CH_TOWN_GATE_OBJ_ID = 2094,
                   WC_TOWN_GATE_OBJ_ID = 2095,
                   HT_TOWN_GATE_OBJ_ID = 2096,
                   SM_TOWN_GATE_OBJ_ID = 19555,
                   EU_TOWN_GATE_OBJ_ID = 19554,
                   SD_S_TOWN_GATE_OBJ_ID = 27753,
                   SD_N_TOWN_GATE_OBJ_ID = 27754,
                   SM_FLYSHIP_OBJ_ID = 7529,
                   EU_FERRY_OBJ_ID = 7524,
                   SM_DROA_OBJ_ID = 7527,
                   SM_SIGIA_OBJ_ID = 7528,
                   AM_FLYSHIP_OBJ_ID = 7547,
                   AM_TUNNEL_S_OBJ_ID = 7546,
                   AM_TUNNEL_N_OBJ_ID = 7545,
                   RM_NW_FLYSHIP_OBJ_ID = 19495,
                   RM_NE_FLYSHIP_OBJ_ID = 5910,
                   RM_SE_FLYSHIP_OBJ_ID = 5911,
                   KK_FLYSHIP_N_OBJ_ID = 5901,
                   KK_FLYSHIP_S_OBJ_ID = 5902,
                   TK_TUNNEL_NW_OBJ_ID = 7548,
                   TK_TUNNEL_SW_OBJ_ID = 7549,
                   KT_FERRY_NE_OBJ_ID = 3826,
                   KT_FERRY_SE_OBJ_ID = 3827,
                   WC_FERRY_NW_OBJ_ID = 3825,
                   WC_FERRY_SW_OBJ_ID = 3824,
                   WC_FERRY_SE_OBJ_ID = 2120,
                   WC_FERRY_NE_OBJ_ID = 2056,
                   CH_FERRY_W_OBJ_ID = 2011,
                   CH_FERRY_E_OBJ_ID = 2119,
                   SD_PAROS_OBJ_ID = 26799,
                   SD_STORM_OBJ_ID = 27755,
                   SD_KINGS_OBJ_ID = 27756,
                   STORM_PORTAL_OBJ_ID = 27757,
                   RED_EGGRE_OBJ_ID = 27751,
                   BLACK_EGGRE_OBJ_ID = 27752;

        public record TeleportEdge(
            string FromRegion,
            string ToRegion,
            BotPosition GateApproachPosition,
            uint GateRefObjId,
            uint TargetTeleportId,
            bool IsPad = false,
            Race RequiredRace = Race.Unknown,  // Unknown = any race allowed
            int MinLevel = 0,                  // 0 = no level requirement
            bool RequiresJobMode = false       // job mode filter not yet enforced — detection TBD
        );

        #endregion

        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"TeleportGraph:{handler}", message); }


        public static readonly List<TeleportEdge> Edges = new()
        {
            #region - Towns -

            new("China", "Donwhang",   BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 2),
            new("China", "Alexandria",  BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 175, MinLevel: 95),
            new("China", "Alexandria",  BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 176, MinLevel: 95),

            new("Donwhang", "China",    BotPosition.FromDisplayWorld(3552, 2082, -106), WC_TOWN_GATE_OBJ_ID, 1),
            new("Donwhang", "Hotan",    BotPosition.FromDisplayWorld(3552, 2082, -106), WC_TOWN_GATE_OBJ_ID, 5),

            new("Hotan", "Donwhang",   BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 2),
            new("Hotan", "Samarakand (Central Asia)",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 25),
            new("Hotan", "Alexandria",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 175, MinLevel: 95),
            new("Hotan", "Alexandria",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 176, MinLevel: 95),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-5185, 2853, 180), SM_TOWN_GATE_OBJ_ID, 20),
            new("Samarakand (Central Asia)", "Hotan",       BotPosition.FromDisplayWorld(-5185, 2853, 180), SM_TOWN_GATE_OBJ_ID, 5),

            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-10688, 2619, 83), EU_TOWN_GATE_OBJ_ID, 25),

            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 22, MinLevel: 95),
            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 23, MinLevel: 95),
            new("Alexandria", "Constantinople", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 21, MinLevel: 95),

            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 23, MinLevel: 95),

            new("Alexandria", "China", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 1, MinLevel: 95),
            new("Alexandria", "Hotan", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 5, MinLevel: 95),
            new("Alexandria", "Alexandria", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 176, MinLevel: 95),

            new("Alexandria", "China", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 1, MinLevel: 95),
            new("Alexandria", "Hotan", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 5, MinLevel: 95),
            new("Alexandria", "Alexandria", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 176, MinLevel: 95),

            #endregion

            #region - Asia -

            // Fly Ships/Ferry npc teleports
            // Asia -----------------------------------------------------------------------------------------------------------
            new("Samarakand (Central Asia)", "Roc Mountain", BotPosition.FromDisplayWorld(-6208, 968, 596), SM_FLYSHIP_OBJ_ID, 31),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-8702, 2217, -9), SM_DROA_OBJ_ID, 21),
            new("Samarakand (Central Asia)", "Alexandria", BotPosition.FromDisplayWorld(-8702, 2217, -9), SM_DROA_OBJ_ID, 177, MinLevel: 95),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-8706, 1850, -4), SM_SIGIA_OBJ_ID, 21),
            new("Samarakand (Central Asia)", "Alexandria", BotPosition.FromDisplayWorld(-8706, 1850, -4), SM_SIGIA_OBJ_ID, 177, MinLevel: 95),

            new("Samarakand (Central Asia)", "Roc Mountain", BotPosition.FromDisplayWorld(-2935, 1884, 299), AM_FLYSHIP_OBJ_ID, 18),
            new("Samarakand (Central Asia)", "Hotan", BotPosition.FromDisplayWorld(-2935, 1884, 299), AM_FLYSHIP_OBJ_ID, 16),

            new("Samarakand (Central Asia)", "Hotan", BotPosition.FromDisplayWorld(-2736, 2110, 184), AM_TUNNEL_S_OBJ_ID, 30),
            new("Samarakand (Central Asia)", "Hotan", BotPosition.FromDisplayWorld(-2784, 2677, 597), AM_TUNNEL_N_OBJ_ID, 29),
            
            #endregion
            
            #region - Roc Mountain -

            // Roc Mountain -----------------------------------------------------------------------------------------------------------
            new("Roc Mountain", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-5765, 640, 3854), RM_NW_FLYSHIP_OBJ_ID, 24),

            new("Roc Mountain", "Hotan", BotPosition.FromDisplayWorld(-3162, 624, 2523), RM_NE_FLYSHIP_OBJ_ID, 16),
            new("Roc Mountain", "Hotan", BotPosition.FromDisplayWorld(-3162, 624, 2523), RM_NE_FLYSHIP_OBJ_ID, 17),
            new("Roc Mountain", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-3162, 624, 2523), RM_NE_FLYSHIP_OBJ_ID, 28),

            new("Roc Mountain", "Hotan", BotPosition.FromDisplayWorld(-3177, -940, 2515), RM_SE_FLYSHIP_OBJ_ID, 16),
            new("Roc Mountain", "Hotan", BotPosition.FromDisplayWorld(-3177, -940, 2515), RM_SE_FLYSHIP_OBJ_ID, 17),
            
            #endregion

            #region - Korakoram -

            // Karakoram --------------------------------------------------------------------------------------------------------------
            new("Hotan", "Roc Mountain", BotPosition.FromDisplayWorld(-2617, 373, 2133), KK_FLYSHIP_N_OBJ_ID, 18),
            new("Hotan", "Roc Mountain", BotPosition.FromDisplayWorld(-2617, 373, 2133), KK_FLYSHIP_N_OBJ_ID, 19),
            new("Hotan", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-2165, 370, 2113), KK_FLYSHIP_N_OBJ_ID, 28),

            new("Hotan", "Roc Mountain", BotPosition.FromDisplayWorld(-2589, -1056, 2044), KK_FLYSHIP_S_OBJ_ID, 18),
            new("Hotan", "Roc Mountain", BotPosition.FromDisplayWorld(-2589, -1056, 2044), KK_FLYSHIP_S_OBJ_ID, 19),

            #endregion

            #region  - Taklamakan -

            // Taklamakan --------------------------------------------------------------------------------------------------------------
            new("Hotan", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-1885, 1977, 393), TK_TUNNEL_NW_OBJ_ID, 26),
            new("Hotan", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-1890, 1391, 247), TK_TUNNEL_SW_OBJ_ID, 27),

            #endregion

            #region - Hotan -

            // Hotan -------------------------------------------------------------------------------------------------------------------
            new("Hotan", "Donwhang", BotPosition.FromDisplayWorld(1060, -56, -28), KT_FERRY_NE_OBJ_ID, 14),
            new("Hotan", "Donwhang", BotPosition.FromDisplayWorld(1102, -305, -34), KT_FERRY_SE_OBJ_ID, 15),

            #endregion

            #region - Western China -

            // Western China
            new("Donwhang", "Hotan", BotPosition.FromDisplayWorld(1588, -17, -20), WC_FERRY_NW_OBJ_ID, 12),
            new("Donwhang", "Hotan", BotPosition.FromDisplayWorld(1582, -295, -57), WC_FERRY_SW_OBJ_ID, 13),

            new("Donwhang", "China", BotPosition.FromDisplayWorld(5041, 1677, 58), WC_FERRY_NE_OBJ_ID, 3),
            new("Donwhang", "China", BotPosition.FromDisplayWorld(4126, 1200, 37), WC_FERRY_SE_OBJ_ID, 9),

            new("Donwhang", "Stone Cave", BotPosition.FromDisplayWorld(2465, 2692, 474), DEFAULT_PAD, DEFAULT_PAD, IsPad: true, MinLevel: 50),

            #endregion

            #region - China -

            new("China", "Donwhang", BotPosition.FromDisplayWorld(5028, 1124, 62), CH_FERRY_E_OBJ_ID, 4),
            new("China", "Donwhang", BotPosition.FromDisplayWorld(4457, 915, 49), CH_FERRY_W_OBJ_ID, 6),
            new("China", "Qin-Shi Tomb|floor:1", BotPosition.FromDisplayWorld(7200, 2104, 150), DEFAULT_PAD, DEFAULT_PAD, IsPad: true, MinLevel: 70),

            #endregion

            #region - Alex -

            new("Alexandria", "Abundance Grounds (Alex Desert)", BotPosition.FromDisplayWorld(-15145, 100, 1212), SD_STORM_OBJ_ID, 180, MinLevel: 95),
            new("Alexandria", "Kings Valley", BotPosition.FromDisplayWorld(-15145, 100, 1212), SD_KINGS_OBJ_ID, 181, MinLevel: 95),

            #endregion          
            
            #region - Europe - 

            // Europe Ferry
            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 22),
            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 23),
            new("Constantinople", "Alexandria", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 177, MinLevel: 95),

            #endregion

            #region - Storm and Cloud Desert -
            
            new("Abundance Grounds (Alex Desert)", "Alexandria", BotPosition.FromDisplayWorld(-13366, -1347, 306), STORM_PORTAL_OBJ_ID, 178),
            
            new("Abundance Grounds (Alex Desert)", "Alexandria Job Cave (Black/Red Eggre)", BotPosition.FromDisplayWorld(-11380, -904, 168), RED_EGGRE_OBJ_ID, 173, IsPad: false, RequiredRace: Race.Chinese,  RequiresJobMode: true),
            new("Abundance Grounds (Alex Desert)", "Alexandria Job Cave (Black/Red Eggre)", BotPosition.FromDisplayWorld(-11397, -1867, 65), BLACK_EGGRE_OBJ_ID, 173, IsPad: false, RequiredRace: Race.European, RequiresJobMode: true),

            #endregion

            #region - Job Cave -

            new("Alexandria Job Cave (Black/Red Eggre)", "Abundance Grounds (Alex Desert)", BotPosition.FromDisplayWorldDungeon(24116, 24257, 57, 0x8010), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Alexandria Job Cave (Black/Red Eggre)", "Abundance Grounds (Alex Desert)", BotPosition.FromDisplayWorldDungeon(25070, 24991, 54, 0x8010), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            #endregion

            #region - Kings Valley -

            //new("Storm Cloud Desert West Portal", "Storm Cloud Desert Entrance", BotPosition.FromDisplayWorld(-13366, -1347, 306), STORM_PORTAL_OBJ_ID, 178),

            #endregion

            #region - Qin-Shit -
            
            // B1
            new("Qin-Shi Tomb|floor:1", "China", BotPosition.FromDisplayWorld(24576, 24231, 54, 0x8007), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            // _1 suffix is each room number starting left -> right, top -> bottom
            // B2
            new("Qin-Shi Tomb|floor:1", "Qin-Shi Tomb|floor:2|room:1", BotPosition.FromDisplayWorld(24576, 25235, 223, 0x8007), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:1", "Qin-Shi Tomb|floor:1", BotPosition.FromDisplayWorld(23197, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 1 -> 2
            new("Qin-Shi Tomb|floor:2|room:1", "Qin-Shi Tomb|floor:2|room:2", BotPosition.FromDisplayWorld(23541, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 2 -> Room 1
            // Room 2 -> Room 7 (Room of Ordeal)
            // Room 2 -> Room 5
            // Room 2 -> Room 3
            // Room 2 -> Room 9
            new("Qin-Shi Tomb|floor:2|room:2", "Qin-Shi Tomb|floor:2|room:1", BotPosition.FromDisplayWorld(23738, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:2", "Qin-Shi Tomb|floor:2|room:7", BotPosition.FromDisplayWorld(23963, 25366, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:2", "Qin-Shi Tomb|floor:2|room:5", BotPosition.FromDisplayWorld(23976, 25366, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:2", "Qin-Shi Tomb|floor:2|room:3", BotPosition.FromDisplayWorld(24201, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:2", "Qin-Shi Tomb|floor:2|room:9", BotPosition.FromDisplayWorld(23970, 24902, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            // Room 3 -> Room 2
            // Room 3 -> Room 4
            // Room 3 -> Room 11
            // Room 3 -> Room 4
            // Room 3 -> Room 10
            new("Qin-Shi Tomb|floor:2|room:3", "Qin-Shi Tomb|floor:2|room:2", BotPosition.FromDisplayWorld(24401, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:3", "Qin-Shi Tomb|floor:2|room:4", BotPosition.FromDisplayWorld(24744, 25142, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:3", "Qin-Shi Tomb|floor:2|room:11", BotPosition.FromDisplayWorld(24744, 25129, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:3", "Qin-Shi Tomb|floor:2|room:4", BotPosition.FromDisplayWorld(24583, 24971, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:3", "Qin-Shi Tomb|floor:2|room:10", BotPosition.FromDisplayWorld(24569, 24971, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 4 -> Room 3
            // Room 4 -> Room 5
            new("Qin-Shi Tomb|floor:2|room:4", "Qin-Shi Tomb|floor:2|room:3", BotPosition.FromDisplayWorld(25196, 25312, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:4", "Qin-Shi Tomb|floor:2|room:5", BotPosition.FromDisplayWorld(25368, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 5 -> Room 4
            // Room 5 -> Room 6
            // Room 5 -> Room 13
            new("Qin-Shi Tomb|floor:2|room:5", "Qin-Shi Tomb|floor:2|room:4", BotPosition.FromDisplayWorld(25613, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:5", "Qin-Shi Tomb|floor:2|room:6", BotPosition.FromDisplayWorld(25960, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:5", "Qin-Shi Tomb|floor:2|room:13", BotPosition.FromDisplayWorld(25788, 24967, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room Of Courage
            // Room 6 -> Room 5
            new("Qin-Shi Tomb|floor:2|room:6", "Qin-Shi Tomb|floor:2|room:5", BotPosition.FromDisplayWorld(26211, 25135, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room Of Ordeal
            // Room 7 -> Room 8
            new("Qin-Shi Tomb|floor:2|room:7", "Qin-Shi Tomb|floor:2|room:8", BotPosition.FromDisplayWorld(22933, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 8 -> Room 9
            // Room 8 -> Room 15
            new("Qin-Shi Tomb|floor:2|room:8", "Qin-Shi Tomb|floor:2|room:9", BotPosition.FromDisplayWorld(23540, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:8", "Qin-Shi Tomb|floor:2|room:15", BotPosition.FromDisplayWorld(23369, 24405, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 9 -> Room 8
            // Room 9 -> Room 10
            new("Qin-Shi Tomb|floor:2|room:9", "Qin-Shi Tomb|floor:2|room:8", BotPosition.FromDisplayWorld(23795, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:9", "Qin-Shi Tomb|floor:2|room:10", BotPosition.FromDisplayWorld(24142, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 10 -> Room 9
            // Room 10 -> Room 11
            new("Qin-Shi Tomb|floor:2|room:10", "Qin-Shi Tomb|floor:2|room:9", BotPosition.FromDisplayWorld(24344, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:10", "Qin-Shi Tomb|floor:2|room:11", BotPosition.FromDisplayWorld(24807, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 11 -> Room 10
            // Room 11 -> Room 12
            // Room 11 -> Room 4
            new("Qin-Shi Tomb|floor:2|room:11", "Qin-Shi Tomb|floor:2|room:10", BotPosition.FromDisplayWorld(25022, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:11", "Qin-Shi Tomb|floor:2|room:12", BotPosition.FromDisplayWorld(25368, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:11", "Qin-Shi Tomb|floor:2|room:4", BotPosition.FromDisplayWorld(25196, 24750, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 12 -> Room 11
            // Room 12 -> Room 5
            new("Qin-Shi Tomb|floor:2|room:12", "Qin-Shi Tomb|floor:2|room:11", BotPosition.FromDisplayWorld(25614, 24574, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:12", "Qin-Shi Tomb|floor:2|room:5", BotPosition.FromDisplayWorld(25788, 24750, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room Of Challenge
            // Room 13 -> Room 12
            // Come back to this

            // Room Of Hero
            // Room 14 -> Room 15
            new("Qin-Shi Tomb|floor:2|room:14", "Qin-Shi Tomb|floor:2|room:15", BotPosition.FromDisplayWorld(22933, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 15 -> Room 8
            // Room 15 -> Room 16
            new("Qin-Shi Tomb|floor:2|room:15", "Qin-Shi Tomb|floor:2|room:8", BotPosition.FromDisplayWorld(23368, 24226, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:15", "Qin-Shi Tomb|floor:2|room:16", BotPosition.FromDisplayWorld(23600, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 16 -> Room 15
            // Room 16 -> Room 14
            // Room 16 -> Room 10
            // Room 16 -> Room 17
            new("Qin-Shi Tomb|floor:2|room:16", "Qin-Shi Tomb|floor:2|room:15", BotPosition.FromDisplayWorld(23795, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:16", "Qin-Shi Tomb|floor:2|room:14", BotPosition.FromDisplayWorld(23962, 24169, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:16", "Qin-Shi Tomb|floor:2|room:10", BotPosition.FromDisplayWorld(23976, 24169, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:16", "Qin-Shi Tomb|floor:2|room:17", BotPosition.FromDisplayWorld(24142, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 17 -> Room 16
            // Room 17 -> Room 2
            // Room 17 -> Room 9
            // Room 17 -> Room 12
            // Room 17 -> Room 4
            // Room 17 -> Room 18
            // Room 17 -> Room 13
            // Room 17 -> Room 15
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:16", BotPosition.FromDisplayWorld(24413, 23987, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:2", BotPosition.FromDisplayWorld(24406, 24002, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:9", BotPosition.FromDisplayWorld(24569, 24167, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:12", BotPosition.FromDisplayWorld(24583, 24167, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:4", BotPosition.FromDisplayWorld(24745, 24001, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:18", BotPosition.FromDisplayWorld(24745, 23987, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:13", BotPosition.FromDisplayWorld(24583, 23830, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:17", "Qin-Shi Tomb|floor:2|room:15", BotPosition.FromDisplayWorld(24569, 23830, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 18 -> Room 17
            // Room 18 -> Room 19
            new("Qin-Shi Tomb|floor:2|room:18", "Qin-Shi Tomb|floor:2|room:17", BotPosition.FromDisplayWorld(25022, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:18", "Qin-Shi Tomb|floor:2|room:19", BotPosition.FromDisplayWorld(25368, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 19 -> Room 18
            // Room 19 -> Room 20
            new("Qin-Shi Tomb|floor:2|room:19", "Qin-Shi Tomb|floor:2|room:18", BotPosition.FromDisplayWorld(25556, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:2|room:19", "Qin-Shi Tomb|floor:2|room:20", BotPosition.FromDisplayWorld(26020, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 20 -> Room 19
            new("Qin-Shi Tomb|floor:2|room:20", "Qin-Shi Tomb|floor:2|room:19", BotPosition.FromDisplayWorld(26212, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // Room 20 -> B3
            new("Qin-Shi Tomb|floor:2|room:20", "Qin-Shi Tomb|floor:3", BotPosition.FromDisplayWorld(26558, 23994, 0, 0x8006), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            

            // B3
            new("Qin-Shi Tomb|floor:3", "Qin-Shi Tomb|floor:2|room:20", BotPosition.FromDisplayWorld(24604, 24596, 0, 0x8005), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // B3 -> B4 TR, BR, BL, TL
            new("Qin-Shi Tomb|floor:3", "Qin-Shi Tomb|floor:4", BotPosition.FromDisplayWorld(26517, 26142, 549, 0x8005), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:3", "Qin-Shi Tomb|floor:4", BotPosition.FromDisplayWorld(26147, 22653, 549, 0x8005), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:3", "Qin-Shi Tomb|floor:4", BotPosition.FromDisplayWorld(22638, 22972, 584, 0x8005), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:3", "Qin-Shi Tomb|floor:4", BotPosition.FromDisplayWorld(23018, 26581, 549, 0x8005), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            
            // B4 -> B3 TR, BR, BL, TL
            new("Qin-Shi Tomb|floor:4", "Qin-Shi Tomb|floor:3", BotPosition.FromDisplayWorld(26436, 26011, -78, 0x8004), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:4", "Qin-Shi Tomb|floor:3", BotPosition.FromDisplayWorld(26123, 22563, -106, 0x8004), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:4", "Qin-Shi Tomb|floor:3", BotPosition.FromDisplayWorld(22757, 23141, -106, 0x8004), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),
            new("Qin-Shi Tomb|floor:4", "Qin-Shi Tomb|floor:3", BotPosition.FromDisplayWorld(23070, 26589, -78, 0x8004), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            #endregion

            #region - Donwhang Cave -

            new("Stone Cave", "Donwhang", BotPosition.FromDisplayWorldDungeon(24689, 24490, 0, 0x8001), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),


            #endregion

        };

        public List<TeleportEdge>? FindShortestRoute(
            string fromRegion,
            string toRegion,
            BotPosition currentPos,
            BotPosition finalDestination,
            Race race = Race.Unknown,
            int level = 0
            )
        {
            static bool Satisfies(string region, string target) =>
                region == target || region.StartsWith(target + "|", StringComparison.Ordinal);

            if (Satisfies(fromRegion, toRegion)) return new List<TeleportEdge>();

            const float HOP_PENALTY = 10_000f; // one hop always costs more than any walk distance

            var best = new Dictionary<string, (float cost, List<TeleportEdge> path)>();
            var queue = new PriorityQueue<(string region, List<TeleportEdge> path, float cost), float>();

            best[fromRegion] = (0f, new List<TeleportEdge>());
            queue.Enqueue((fromRegion, new List<TeleportEdge>(), 0f), 0f);

            while (queue.Count > 0)
            {
                var (region, path, cost) = queue.Dequeue();

                if (Satisfies(region, toRegion))
                    return path;

                if (best.TryGetValue(region, out var b) && b.cost < cost)
                    continue;

                foreach (var edge in Edges.Where(e =>
                    e.FromRegion == region &&
                    (e.RequiredRace == Race.Unknown || e.RequiredRace == race) &&
                    (e.MinLevel == 0 || level >= e.MinLevel)))
                {
                    var approachFrom = path.Count == 0
                        ? currentPos
                        : GetLandingPosition(path.Last(), currentPos);

                    float walkCost = Distance(approachFrom, edge.GateApproachPosition);
                    float totalCost = cost + HOP_PENALTY + walkCost;

                    if (!best.TryGetValue(edge.ToRegion, out var existing) || totalCost < existing.cost)
                    {
                        var newPath = new List<TeleportEdge>(path) { edge };
                        best[edge.ToRegion] = (totalCost, newPath);
                        queue.Enqueue((edge.ToRegion, newPath, totalCost), totalCost);
                    }
                }
            }

            return null;
        }

        private BotPosition GetLandingPosition(TeleportEdge edge, BotPosition fallback)
        {
            var returnEdge = Edges.FirstOrDefault(e => e.FromRegion == edge.ToRegion);
            return returnEdge != null ? returnEdge.GateApproachPosition : fallback;
        }

        private static float Distance(BotPosition a, BotPosition b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
