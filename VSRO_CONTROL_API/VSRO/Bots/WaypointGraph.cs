using CoreLib.Tools.Logging;
using VSRO_CONTROL.NavMeshApi;
using VSRO_CONTROL.NavMeshApi.Mathematics;
using System.Numerics;
using VSRO_CONTROL_API.VSRO.Tools;

public class WaypointGraph
{
    public record Node(string Id, BotPosition Position);
    public record Edge(string FromId, string ToId);

    private readonly Dictionary<string, Node> _nodes = new();
    private readonly List<Edge> _edges = new();

    public void AddNode(string id, BotPosition pos) =>
        _nodes[id] = new Node(id, pos);

    public void AddEdge(string fromId, string toId)
    {
        _edges.Add(new Edge(fromId, toId));
        _edges.Add(new Edge(toId, fromId));
    }

    public List<Node>? FindPath(BotPosition from, BotPosition to)
    {
        var startNode = GetClosestNode(from, to);  // pass destination
        var endNode = GetClosestNode(to, from);

        Logger.Info("WaypointGraph", $"Start node: {startNode?.Id}");
        Logger.Info("WaypointGraph", $"End node: {endNode?.Id}");

        if (startNode == null || endNode == null) return null;
        return AStar(startNode, endNode);
    }

    private Node? GetClosestNode(BotPosition from, BotPosition destination)
    {
        float destAngle = MathF.Atan2(
            destination.Y - from.Y,
            destination.X - from.X
        );

        foreach (float radius in new[] { 300f, 600f, 1200f })
        {
            var fromRegion = RegionResolver.Resolve((short)from.RegionId, from.SectorX, from.SectorY);
            var candidate = _nodes.Values
                .Where(n => Distance(n.Position, from) <= radius)
                //.Where(n => CanReach(from, n.Position))
                .Where(n => {
                    // Please dont crucify me, i may have been drunk
                    if (fromRegion.Contains("Qin-Shi Tomb|floor:1"))
                    {
                        return n.Id.Contains("qs1_");
                    }
                    else if (fromRegion.Contains("Qin-Shi Tomb|floor:2"))
                    {
                        if (fromRegion.Contains("|room:1"))
                        {
                            return n.Id.Contains("qs2_1_");
                        } 
                        else if (fromRegion.Contains("|room:2"))
                        {
                            return n.Id.Contains("qs2_2_");
                        }
                        else if (fromRegion.Contains("|room:3"))
                        {
                            return n.Id.Contains("qs2_3_");
                        }
                        else if (fromRegion.Contains("|room:4"))
                        {
                            return n.Id.Contains("qs2_4_");
                        }
                        else if (fromRegion.Contains("|room:5"))
                        {
                            return n.Id.Contains("qs2_5_");
                        }
                        else if (fromRegion.Contains("|room:6"))
                        {
                            return n.Id.Contains("qs2_6_");
                        }
                        else if (fromRegion.Contains("|room:7"))
                        {
                            return n.Id.Contains("qs2_7_");
                        }
                        else if (fromRegion.Contains("|room:8"))
                        {
                            return n.Id.Contains("qs2_8_");
                        }
                        else if (fromRegion.Contains("|room:9"))
                        {
                            return n.Id.Contains("qs2_9_");
                        }
                        else if (fromRegion.Contains("|room:10"))
                        {
                            return n.Id.Contains("qs2_10_");
                        }
                        else if (fromRegion.Contains("|room:11"))
                        {
                            return n.Id.Contains("qs2_11_");
                        }
                        else if (fromRegion.Contains("|room:12"))
                        {
                            return n.Id.Contains("qs2_12_");
                        }
                        else if (fromRegion.Contains("|room:13"))
                        {
                            return n.Id.Contains("qs2_13_");
                        }
                        else if (fromRegion.Contains("|room:14"))
                        {
                            return n.Id.Contains("qs2_14_");
                        }
                        else if (fromRegion.Contains("|room:15"))
                        {
                            return n.Id.Contains("qs2_15_");
                        }
                        else if (fromRegion.Contains("|room:16"))
                        {
                            return n.Id.Contains("qs2_16_");
                        }
                        else if (fromRegion.Contains("|room:17"))
                        {
                            return n.Id.Contains("qs2_17_");
                        }
                        else if (fromRegion.Contains("|room:18"))
                        {
                            return n.Id.Contains("qs2_18_");
                        }
                        else if (fromRegion.Contains("|room:19"))
                        {
                            return n.Id.Contains("qs2_19_");
                        }
                        else if (fromRegion.Contains("|room:20"))
                        {
                            return n.Id.Contains("qs2_20_");
                        }

                        return n.Id.Contains("qs2_");
                    }
                    else if (fromRegion.Contains("Qin-Shi Tomb|floor:3"))
                    {
                        return n.Id.Contains("qs3_");
                    }
                    else if (fromRegion.Contains("Qin-Shi Tomb|floor:4"))
                    {
                        return n.Id.Contains("qs4_");
                    }
                    else if (fromRegion.Contains("Qin-Shi Tomb|floor:5"))
                    {
                        return n.Id.Contains("qs5_");
                    }
                    else if (fromRegion.Contains("Qin-Shi Tomb|floor:6"))
                    {
                        return n.Id.Contains("qs6_");
                    }
                    else if (fromRegion.Contains("Stone Cave"))
                    {
                        return n.Id.Contains("sc_");
                    }
                    else if (fromRegion.Contains("Alexandria Job Cave (Black/Red Eggre)"))
                    {
                        return n.Id.Contains("jc_");
                    }

                    return true;
                })
                .OrderBy(n =>
                {
                    float dist = Distance3D(n.Position, from);
                    float nodeAngle = MathF.Atan2(
                        n.Position.Y - from.Y,
                        n.Position.X - from.X
                    );
                    float angleDiff = MathF.Abs(AngleDelta(destAngle, nodeAngle));
                    float penalty = angleDiff > MathF.PI / 2 ? 2.5f : 1.0f;
                    return dist * penalty;
                })
                .FirstOrDefault();

            if (candidate != null) return candidate;
        }
        return null;
    }

    public static float Distance3D(BotPosition a, BotPosition b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.ZOffset - b.ZOffset;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float AngleDelta(float a, float b)
    {
        float d = a - b;
        while (d > MathF.PI) d -= 2 * MathF.PI;
        while (d < -MathF.PI) d += 2 * MathF.PI;
        return d;
    }

    private List<Node>? AStar(Node start, Node goal)
    {
        var open = new PriorityQueue<Node, float>();
        var cameFrom = new Dictionary<string, string>();
        var gScore = new Dictionary<string, float>();

        gScore[start.Id] = 0;
        open.Enqueue(start, 0);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current.Id == goal.Id)
                return Reconstruct(cameFrom, current);

            foreach (var edge in _edges.Where(e => e.FromId == current.Id))
            {
                var neighbor = _nodes[edge.ToId];
                float g = gScore.GetValueOrDefault(current.Id, float.MaxValue)
                        + Distance(current.Position, neighbor.Position);

                if (g < gScore.GetValueOrDefault(neighbor.Id, float.MaxValue))
                {
                    cameFrom[neighbor.Id] = current.Id;
                    gScore[neighbor.Id] = g;
                    float f = g + Distance(neighbor.Position, goal.Position);
                    open.Enqueue(neighbor, f);
                }
            }
        }
        return null;
    }

    private List<Node> Reconstruct(Dictionary<string, string> cameFrom, Node current)
    {
        var path = new List<Node>();
        while (cameFrom.TryGetValue(current.Id, out var prevId))
        {
            path.Add(current);
            current = _nodes[prevId];
        }
        path.Add(current);
        path.Reverse();
        return path;
    }

    private static NavMeshTransform ToTransform(BotPosition pos) =>
        new NavMeshTransform(new RID(pos.RegionId),
            new Vector3(pos.XOffset, pos.ZOffset, pos.YOffset));

    private static float Distance(BotPosition a, BotPosition b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private bool CanReach(BotPosition from, BotPosition to)
    {
        try
        {
            var srcT = ToTransform(from);
            var dstT = ToTransform(to);
            if (!NavMeshManager.ResolveCellAndHeight(srcT)) return false;
            if (!NavMeshManager.ResolveCellAndHeight(dstT)) return false;
            return NavMeshManager.Raycast(srcT, dstT, NavMeshRaycastType.Move);
        }
        catch { return false; }
    }
}