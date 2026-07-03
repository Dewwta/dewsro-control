using CoreLib.Tools.Logging;
using VSRO_CONTROL.NavMeshApi;
using VSRO_CONTROL.NavMeshApi.Mathematics;
using System.Numerics;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class NavMeshPathfinder
    {
        
        public static List<BotPosition> StringPull(BotPosition from, BotPosition to, bool skipCollision = false)
        {
            //Logger.Trace("StringPull", $"=== StringPull START ===");
            //Logger.Trace("StringPull", $"  From : region=0x{from.RegionId:X4} world=({from.X:F1},{from.Y:F1})");
            //Logger.Trace("StringPull", $"  To   : region=0x{to.RegionId:X4} world=({to.X:F1},{to.Y:F1})");

            // Initialize the path with the starting node
            var result = new List<BotPosition> { from };

            result.Add(to);

  
            return result;
        }
        public static NavMeshTransform MakeTransform(BotPosition pos)
        {
            return new NavMeshTransform(
                new RID(pos.RegionId),
                new Vector3(pos.XOffset, pos.ZOffset, pos.YOffset)
            );
        }

        public static BotPosition OffsetToBotPosition(Vector3 offset, RID region)
        {
            return new BotPosition
            {
                RegionId = (ushort)region,
                XOffset = offset.X,
                ZOffset = offset.Y,  // NavMesh Y axis represents game height elevation
                YOffset = offset.Z   // NavMesh Z axis represents game horizontal Y grid axis
            };
        }

        #region - Deprecated -

        // Maximum sub-points the string puller will generate per leg.
        private const int MAX_PULL_ITERATIONS = 30;

        // Increased to accommodate the 10x offset scaling. 
        // 50f raw offset units = 5.0f units of actual game world movement space.
        private const float EDGE_NUDGE = 50f;

        private const float ARRIVAL_THRESHOLD = 1f;

        [Obsolete]
        private static float Distance(BotPosition a, BotPosition b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        [Obsolete]
        private static float ForwardScore(BotPosition from, BotPosition candidate, BotPosition goal)
        {
            float toGoalX = goal.X - from.X;
            float toGoalY = goal.Y - from.Y;
            float lenA = MathF.Sqrt(toGoalX * toGoalX + toGoalY * toGoalY);

            float toCandX = candidate.X - from.X;
            float toCandY = candidate.Y - from.Y;
            float lenB = MathF.Sqrt(toCandX * toCandX + toCandY * toCandY);

            if (lenA < 0.001f || lenB < 0.001f)
                return -1f;

            toGoalX /= lenA;
            toGoalY /= lenA;
            toCandX /= lenB;
            toCandY /= lenB;

            return toGoalX * toCandX + toGoalY * toCandY;
        }
        #endregion
    }


}