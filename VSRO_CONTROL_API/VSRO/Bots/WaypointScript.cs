using VSRO_CONTROL_API.VSRO.Bots.DTO;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    public class WaypointScript
    {
        public string Name { get; set; } = "";
        public List<Waypoint> Points { get; set; } = new();

        public static WaypointScript Load(string path)
        {
            var script = new WaypointScript
            {
                Name = Path.GetFileNameWithoutExtension(path)
            };

            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                var parts = line.Split(',');
                if (parts[0].Trim().ToLower() != "move") continue;

                if (parts.Length == 3
                    && int.TryParse(parts[1], out int wx)
                    && int.TryParse(parts[2], out int wy))
                {
                    script.Points.Add(new Waypoint { WorldX = wx, WorldY = wy });
                }
                else if (parts.Length == 5
                    && short.TryParse(parts[1], out short region)
                    && short.TryParse(parts[2], out short rx)
                    && short.TryParse(parts[3], out short ry)
                    && short.TryParse(parts[4], out short z))
                {
                    script.Points.Add(new Waypoint
                    {
                        RegionID = region,
                        RawX = rx,
                        RawY = ry,
                        Z = z,
                        WorldX = RegionToWorld(region, rx),
                        WorldY = RegionToWorld(region, ry)
                    });
                }
            }
            return script;
        }

        // Nearest point to start from (for joining mid-route)
        public int GetNearestIndex(int worldX, int worldY, int maxRange)
        {
            int best = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < Points.Count; i++)
            {
                double d = Distance(worldX, worldY, Points[i].WorldX, Points[i].WorldY);
                if (d < bestDist && d <= maxRange)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        private static double Distance(int x1, int y1, int x2, int y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // VSRO sector-relative to world coords
        // Each region/sector is 1920 units wide
        private static int RegionToWorld(short regionID, short raw)
        {
            // High byte = sector index, raw is offset within sector
            // RegionID encodes sectorX in high byte, sectorY in low byte
            // This gives you the flat world coordinate
            int sector = (regionID >> 8) & 0xFF; // for X; use low byte for Y
            return sector * 1920 + raw;
        }
    }

    
}
