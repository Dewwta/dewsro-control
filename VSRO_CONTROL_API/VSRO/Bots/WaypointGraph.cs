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
        // startNode: closest node to bot's current position (pure distance, no direction bias).
        // Direction bias caused the algorithm to pick nodes across obstacles — A* handles routing.
        var startNode = GetClosestNode(from, to, destWeight: 0f);

        // For dungeon regions, the endNode must be in the same room as the startNode.
        // Without this constraint, a destination that sits right at a room boundary resolves
        // via RegionResolver to the *adjacent* room, producing a cross-room A* pair that has
        // no edges and always falls back to a direct (graphless) walk.
        // We extract the room prefix from startNode.Id (e.g. "qs2_15_" from "qs2_15_4") and
        // pass it as an override so the endNode search is filtered to the same room.
        string? roomOverride = null;
        if (startNode != null && (from.RegionId & 0x8000) != 0)
        {
            var parts = startNode.Id.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[1], out _))
                roomOverride = $"{parts[0]}_{parts[1]}_";   // e.g. "qs2_15_"
        }

        var endNode = GetClosestNode(to, from, destWeight: 0.3f, roomOverride: roomOverride);

        Logger.Info("WaypointGraph", $"Start node: {startNode?.Id}");
        Logger.Info("WaypointGraph", $"End node: {endNode?.Id}");

        if (startNode == null || endNode == null) return null;
        return AStar(startNode, endNode);
    }

    // Scores each candidate as dist(from, node) + destWeight * dist(node, destination).
    // destWeight=0   for startNode: pure closest node to bot — safe to walk to directly.
    // destWeight=0.3 for endNode:   closest to dest, mild bias away from bot-side nodes.
    // roomOverride: when set, bypasses region-derived room filter and uses this prefix instead.
    private Node? GetClosestNode(BotPosition from, BotPosition destination, float destWeight, string? roomOverride = null)
    {
        foreach (float radius in new[] { 300f, 600f, 1200f })
        {
            var fromRegion = ((from.RegionId & 0x8000) != 0)
                ? RegionResolver.Resolve((short)from.RegionId, (int)(from.X / 192), (int)(from.Y / 192))
                : RegionResolver.Resolve((short)from.RegionId, from.SectorX, from.SectorY);
            var candidate = _nodes.Values
                .Where(n => Distance(n.Position, from) <= radius)
                //.Where(n => CanReach(from, n.Position))
                .Where(n => {
                    // If the caller pinned a specific room prefix, honour it unconditionally.
                    if (roomOverride != null)
                        return n.Id.StartsWith(roomOverride);

                    if (fromRegion.Contains("Qin-Shi Tomb|floor:1"))
                        return n.Id.StartsWith("qs1_");

                    if (fromRegion.Contains("Qin-Shi Tomb|floor:2"))
                    {
                        for (int r = 20; r >= 1; r--)
                        {
                            if (fromRegion.Contains($"|room:{r}"))
                                return n.Id.StartsWith($"qs2_{r}_");
                        }
                        return n.Id.StartsWith("qs2_");
                    }

                    if (fromRegion.Contains("Qin-Shi Tomb|floor:3")) return n.Id.StartsWith("qs3_");
                    if (fromRegion.Contains("Qin-Shi Tomb|floor:4")) return n.Id.StartsWith("qs4_");
                    if (fromRegion.Contains("Qin-Shi Tomb|floor:5")) return n.Id.StartsWith("qs5_");
                    if (fromRegion.Contains("Qin-Shi Tomb|floor:6")) return n.Id.StartsWith("qs6_");
                    if (fromRegion.Contains("Stone Cave")) return n.Id.StartsWith("sc_");
                    if (fromRegion.Contains("Alexandria Job Cave (Black/Red Eggre)")) return n.Id.StartsWith("jc_");

                    return true;
                })
                .OrderBy(n => Distance3D(n.Position, from) + destWeight * Distance(n.Position, destination))
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

    [Obsolete]
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