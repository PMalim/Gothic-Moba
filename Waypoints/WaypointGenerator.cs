using BotArena.Nav;

namespace BotArena.Waypoints;

public static class WaypointGenerator
{
    public const float LaneStep = 700f, Spacing = 1300f, LinkRange = 2400f;
    private const int MaxLinks = 5;

    public static void Generate(WaypointGraph graph, NavGrid grid, NavRouter router, Func<Position, Position, bool> visible, IEnumerable<(string Name, Position[] Points)> lanes)
    {
        graph.Clear();
        var sequential = new List<(int, int)>();
        var laneNodes = new HashSet<int>();

        foreach ((string lane, Position[] points) in lanes)
        {
            int previous = -1;
            for (int i = 0; i < points.Length; i++)
            {
                string key = i == 0 ? $"{lane}-Start-N" : i == points.Length - 1 ? $"{lane}-Start-C" : $"{lane}-P{i}";
                WaypointNode? anchor = graph.Add(key, points[i].X, Ground(grid, points[i]), points[i].Z);
                if (anchor is null) continue;
                laneNodes.Add(anchor.Id);
                if (previous >= 0)
                {
                    WaypointNode a = graph.Find(previous)!;
                    var corridor = new List<Position> { a.At };
                    if (router.Route(a.At, anchor.At, 400) is { } route) corridor.AddRange(route);
                    else corridor.Add(anchor.At);
                    int last = previous;
                    float total = 0f;
                    for (int s = 1; s < corridor.Count; s++) total += Dist(corridor[s - 1], corridor[s]);
                    int inner = Math.Max(0, (int)MathF.Round(total / LaneStep) - 1);
                    for (int k = 1; k <= inner; k++)
                    {
                        Position p = AlongPolyline(corridor, total * k / (inner + 1));
                        WaypointNode? mid = graph.Add($"{lane}-{i}.{k}", p.X, Ground(grid, p), p.Z);
                        if (mid is null) break;
                        laneNodes.Add(mid.Id);
                        sequential.Add((last, mid.Id));
                        last = mid.Id;
                    }
                    sequential.Add((last, anchor.Id));
                }
                previous = anchor.Id;
            }
        }

        var taken = graph.Nodes.Select(n => n.At).ToList();
        int stride = Math.Max(1, (int)(Spacing / grid.Cell / 3));
        int terrain = 0;
        for (int j = 0; j < grid.Height; j += stride)
            for (int i = 0; i < grid.Width; i += stride)
            {
                int n = grid.Index(i, j);
                if (System.Numerics.BitOperations.PopCount(grid.Mask[n]) < 6 || grid.Cost[n] == NavCost.Blocked || grid.Ground[n] == short.MinValue) continue;
                Position p = grid.Center(i, j);
                if (taken.Any(o => Dist(o, p) < Spacing)) continue;
                if (graph.Add($"Teren-{++terrain}", p.X, p.Y, p.Z) is { } node) taken.Add(node.At);
            }

        foreach ((int a, int b) in sequential) graph.Link(a, b);

        List<WaypointNode> all = [.. graph.Nodes];
        foreach (WaypointNode node in all)
        {
            int made = 0;
            foreach (WaypointNode other in all.Where(o => o.Id != node.Id && Dist(node.At, o.At) <= LinkRange).OrderBy(o => Dist(node.At, o.At)))
            {
                if (made >= MaxLinks) break;
                if (graph.HasLink(node.Id, other.Id)) { made++; continue; }
                if (laneNodes.Contains(node.Id) && laneNodes.Contains(other.Id) && NameLane(node.Name) == NameLane(other.Name)) continue;
                if (!visible(node.At, other.At)) continue;
                graph.Link(node.Id, other.Id);
                made++;
            }
        }
        JoinOrDrop(graph, visible);
    }

    private static List<List<int>> Components(WaypointGraph graph)
    {
        var seen = new HashSet<int>();
        var result = new List<List<int>>();
        foreach (WaypointNode node in graph.Nodes)
        {
            if (!seen.Add(node.Id)) continue;
            var members = new List<int> { node.Id };
            for (int at = 0; at < members.Count; at++)
                foreach (int next in graph.Neighbors(members[at])) if (seen.Add(next)) members.Add(next);
            result.Add(members);
        }
        return [.. result.OrderByDescending(c => c.Count)];
    }

    private static void JoinOrDrop(WaypointGraph graph, Func<Position, Position, bool> visible)
    {
        List<List<int>> parts = Components(graph);
        if (parts.Count <= 1) return;
        var main = new HashSet<int>(parts[0]);
        foreach (List<int> part in parts.Skip(1))
        {
            (int A, int B, float D)? best = null;
            foreach (int a in part)
                foreach (int b in main)
                {
                    float d = Dist(graph.Find(a)!.At, graph.Find(b)!.At);
                    if (d > BridgeRange || (best is { } known && known.D <= d) || !visible(graph.Find(a)!.At, graph.Find(b)!.At)) continue;
                    best = (a, b, d);
                }
            if (best is { } link)
            {
                graph.Link(link.A, link.B);
                foreach (int id in part) main.Add(id);
            }
            else foreach (int id in part) graph.Remove(id);
        }
    }

    private const float BridgeRange = 5000f;

    private static string NameLane(string name) { int cut = name.IndexOf('-'); return cut < 0 ? name : name[..cut]; }

    private static float Ground(NavGrid grid, Position p) => grid.HeightAt(p.X, p.Z) ?? p.Y;

    private static float Dist(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static Position AlongPolyline(List<Position> line, float distance)
    {
        for (int s = 1; s < line.Count; s++)
        {
            float length = Dist(line[s - 1], line[s]);
            if (distance <= length || s == line.Count - 1) return Lerp(line[s - 1], line[s], length < 1f ? 0f : Math.Clamp(distance / length, 0f, 1f));
            distance -= length;
        }
        return line[^1];
    }

    private static Position Lerp(Position a, Position b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
}
