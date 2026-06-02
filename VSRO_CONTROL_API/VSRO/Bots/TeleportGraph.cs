namespace VSRO_CONTROL_API.VSRO.Bots
{
    public class TeleportGraph
    {
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
                   STORM_PORTAL_OBJ_ID = 27757;

        public record TeleportEdge(
            string FromRegion,
            string ToRegion,
            BotPosition GateApproachPosition,
            uint GateRefObjId,
            uint TargetTeleportId,
            bool IsPad = false              // true = walk onto pad, no packet needed
        );

        public static readonly List<TeleportEdge> Edges = new()
        {
            #region - Towns -

            new("China", "Donwhang",   BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 2),
            new("China", "Alexandria",  BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 175),
            new("China", "Alexandria",  BotPosition.FromDisplayWorld(6454, 1091, -33), CH_TOWN_GATE_OBJ_ID, 176),

            new("Donwhang", "China",    BotPosition.FromDisplayWorld(3552, 2082, -106), WC_TOWN_GATE_OBJ_ID, 1),
            new("Donwhang", "Hotan",    BotPosition.FromDisplayWorld(3552, 2082, -106), WC_TOWN_GATE_OBJ_ID, 5),

            new("Hotan", "Donwhang",   BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 2),
            new("Hotan", "Samarakand (Central Asia)",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 25),
            new("Hotan", "Alexandria",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 175),
            new("Hotan", "Alexandria",  BotPosition.FromDisplayWorld(119, 32, 244), HT_TOWN_GATE_OBJ_ID, 176),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-5185, 2853, 180), SM_TOWN_GATE_OBJ_ID, 20),
            new("Samarakand (Central Asia)", "Hotan",       BotPosition.FromDisplayWorld(-5185, 2853, 180), SM_TOWN_GATE_OBJ_ID, 5),

            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-10688, 2619, 83), EU_TOWN_GATE_OBJ_ID, 25),

            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 22),
            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 23),
            new("Alexandria", "Constantinople", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 21),

            new("Alexandria", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-16546, 364, 584), SD_PAROS_OBJ_ID, 23),

            new("Alexandria", "China", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 1),
            new("Alexandria", "Hotan", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 5),
            new("Alexandria", "Alexandria", BotPosition.FromDisplayWorld(-16622, -296, 862), SD_S_TOWN_GATE_OBJ_ID, 176),

            new("Alexandria", "China", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 1),
            new("Alexandria", "Hotan", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 5),
            new("Alexandria", "Alexandria", BotPosition.FromDisplayWorld(-16151, 54, 1519), SD_N_TOWN_GATE_OBJ_ID, 176),

            #endregion

            #region - Asia -

            // Fly Ships/Ferry npc teleports
            // Asia -----------------------------------------------------------------------------------------------------------
            new("Samarakand (Central Asia)", "Roc Mountain", BotPosition.FromDisplayWorld(-6208, 968, 596), SM_FLYSHIP_OBJ_ID, 31),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-8702, 2217, -9), SM_DROA_OBJ_ID, 21),
            new("Samarakand (Central Asia)", "Alexandria", BotPosition.FromDisplayWorld(-8702, 2217, -9), SM_DROA_OBJ_ID, 177),

            new("Samarakand (Central Asia)", "Constantinople", BotPosition.FromDisplayWorld(-8706, 1850, -4), SM_SIGIA_OBJ_ID, 21),
            new("Samarakand (Central Asia)", "Alexandria", BotPosition.FromDisplayWorld(-8706, 1850, -4), SM_SIGIA_OBJ_ID, 177),

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

            new("Donwhang", "Stone Cave", BotPosition.FromDisplayWorld(2465, 2692, 474), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            #endregion

            #region - China -

            new("China", "Donwhang", BotPosition.FromDisplayWorld(5028, 1124, 62), CH_FERRY_E_OBJ_ID, 4),
            new("China", "Donwhang", BotPosition.FromDisplayWorld(4457, 915, 49), CH_FERRY_W_OBJ_ID, 6),

            #endregion

            #region - Alex -

            new("Alexandria", "Abundance Grounds (Alex Desert)", BotPosition.FromDisplayWorld(-15145, 100, 1212), SD_STORM_OBJ_ID, 180),
            new("Alexandria", "Kings Valley", BotPosition.FromDisplayWorld(-15145, 100, 1212), SD_KINGS_OBJ_ID, 181),
            

            #endregion          
            
            #region - Europe - 

            // Europe Ferry
            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 22),
            new("Constantinople", "Samarakand (Central Asia)", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 23),
            new("Constantinople", "Alexandria", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 177),

            #endregion

            #region - Storm and Cloud Desert -
            
            new("Abundance Grounds (Alex Desert)", "Alexandria", BotPosition.FromDisplayWorld(-13366, -1347, 306), STORM_PORTAL_OBJ_ID, 178),
            new("Abundance Grounds (Alex Desert)", "Alexandria Job Cave (Black/Red Eggre)", BotPosition.FromDisplayWorld(-13366, -1347, 306), STORM_PORTAL_OBJ_ID, 178),

            // Add job caves when the dungeon coords are working, unsure if they are, its not clear without any reference coords, need to use sbot to figure this out.

            #endregion

            #region - Job Cave -

            new("Alexandria Job Cave (Black/Red Eggre)", "Abundance Grounds (Alex Desert)", BotPosition.FromDisplayWorldDungeon(24117, 24258, 57, 0x8010), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),

            #endregion

            #region - Kings Valley -

            //new("Storm Cloud Desert West Portal", "Storm Cloud Desert Entrance", BotPosition.FromDisplayWorld(-13366, -1347, 306), STORM_PORTAL_OBJ_ID, 178),

            #endregion

            #region - Qin-Shit -
            // _1 suffix is each room number starting left -> right, top -> bottom
            new("Qin-Shi Tomb|floor:1", "Qin-Shi Tomb|floor:2_1", BotPosition.FromDisplayWorld(-11443, 1170, -184), EU_FERRY_OBJ_ID, 177),

            

            #endregion

            #region - Donwhang Cave -

            new("Stone Cave", "Donwhang", BotPosition.FromDisplayWorldDungeon(24689, 24490, 0, 0x8001), DEFAULT_PAD, DEFAULT_PAD, IsPad: true),


            #endregion

        };

        public List<TeleportEdge>? FindShortestRoute(string fromRegion, string toRegion, BotPosition currentPos, BotPosition finalDestination)
        {
            if (fromRegion == toRegion) return new List<TeleportEdge>();

            // For each node, track best (cost, path)
            var best = new Dictionary<string, (float cost, List<TeleportEdge> path)>();
            var queue = new PriorityQueue<(string region, List<TeleportEdge> path, float cost), float>();

            best[fromRegion] = (0, new List<TeleportEdge>());
            queue.Enqueue((fromRegion, new List<TeleportEdge>(), 0), 0);

            while (queue.Count > 0)
            {
                var (region, path, cost) = queue.Dequeue();

                if (region == toRegion)
                    return path;

                // Skip if we already found a cheaper way here
                if (best.TryGetValue(region, out var b) && b.cost < cost)
                    continue;

                foreach (var edge in Edges.Where(e => e.FromRegion == region))
                {
                    // Cost = distance from current position (or last landing) to this gate approach
                    var approachFrom = path.Count == 0 ? currentPos : GetLandingPosition(path.Last(), currentPos);
                    float walkCost = Distance(approachFrom, edge.GateApproachPosition);

                    // After teleporting, how far are we from the final destination?
                    // This is the heuristic — prefers routes that land closer to the goal
                    var landingPos = GetLandingPosition(edge, finalDestination);
                    float heuristic = Distance(landingPos, finalDestination);

                    float totalCost = cost + walkCost + heuristic * 0.5f;

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
