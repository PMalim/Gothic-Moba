namespace BotArena.Waypoints;

public sealed class WaypointRouter(WaypointGraph graph, Func<Position, Position, bool> visible)
{
    private const int Candidates = 10, MaxEnds = 3;
    private const float StraightEnough = 0.97f;

    private static float Dist(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public IReadOnlyList<Position>? Route(Position from, Position to, int maxPoints = 48)
    {
        if (graph.Count == 0) return null;
        if (visible(from, to)) return [to];
        List<WaypointNode> starts = Ends(from, fromStart: true), goals = Ends(to, fromStart: false);
        if (starts.Count == 0 || goals.Count == 0) return null;

        var g = new Dictionary<int, float>();
        var parent = new Dictionary<int, int>();
        var open = new PriorityQueue<int, float>();
        var goalSet = goals.Select(n => n.Id).ToHashSet();
        foreach (WaypointNode s in starts)
        {
            g[s.Id] = Dist(from, s.At);
            parent[s.Id] = -1;
            open.Enqueue(s.Id, g[s.Id] + Dist(s.At, to));
        }
        var closed = new HashSet<int>();
        int bestGoal = -1;
        float bestTotal = float.MaxValue;
        while (open.TryDequeue(out int id, out float f))
        {
            if (f >= bestTotal) break;
            if (!closed.Add(id)) continue;
            WaypointNode node = graph.Find(id)!;
            if (goalSet.Contains(id))
            {
                float total = g[id] + Dist(node.At, to);
                if (total < bestTotal) { bestTotal = total; bestGoal = id; }
            }
            foreach (int next in graph.Neighbors(id))
            {
                if (closed.Contains(next) || graph.Find(next) is not { } other) continue;
                float cost = g[id] + Dist(node.At, other.At);
                if (g.TryGetValue(next, out float known) && known <= cost) continue;
                g[next] = cost;
                parent[next] = id;
                open.Enqueue(next, cost + Dist(other.At, to));
            }
        }
        if (bestGoal < 0) return null;

        var chain = new List<Position>();
        for (int id = bestGoal; id >= 0; id = parent[id]) chain.Add(graph.Find(id)!.At);
        chain.Reverse();
        chain.Add(to);
        return Thin(Pull(from, chain), maxPoints);
    }

    public List<int>? RouteNodes(int a, int b)
    {
        if (graph.Find(a) is null || graph.Find(b) is null) return null;
        if (a == b) return [a];
        var g = new Dictionary<int, float> { [a] = 0 };
        var parent = new Dictionary<int, int> { [a] = -1 };
        var open = new PriorityQueue<int, float>();
        open.Enqueue(a, 0);
        var closed = new HashSet<int>();
        Position target = graph.Find(b)!.At;
        while (open.TryDequeue(out int id, out _))
        {
            if (!closed.Add(id)) continue;
            if (id == b) break;
            Position at = graph.Find(id)!.At;
            foreach (int next in graph.Neighbors(id))
            {
                if (closed.Contains(next) || graph.Find(next) is not { } other) continue;
                float cost = g[id] + Dist(at, other.At);
                if (g.TryGetValue(next, out float known) && known <= cost) continue;
                g[next] = cost;
                parent[next] = id;
                open.Enqueue(next, cost + Dist(other.At, target));
            }
        }
        if (!parent.ContainsKey(b)) return null;
        var path = new List<int>();
        for (int id = b; id >= 0; id = parent[id]) path.Add(id);
        path.Reverse();
        return path;
    }

    private List<WaypointNode> Ends(Position p, bool fromStart)
    {
        List<WaypointNode> near = [.. graph.Nodes.OrderBy(n => Dist(p, n.At)).Take(Candidates)];
        List<WaypointNode> seen = [.. near.Where(n => fromStart ? visible(p, n.At) : visible(n.At, p)).Take(MaxEnds)];
        return seen.Count > 0 ? seen : [];
    }

    private List<Position> Pull(Position from, List<Position> chain)
    {
        var all = new List<Position>(chain.Count + 1) { from };
        all.AddRange(chain);
        var kept = new List<Position>();
        int i = 0;
        while (i < all.Count - 1)
        {
            int next = i + 1;
            float walked = Dist(all[i], all[i + 1]);
            for (int j = i + 2; j < all.Count; j++)
            {
                walked += Dist(all[j - 1], all[j]);
                if (Dist(all[i], all[j]) >= StraightEnough * walked && visible(all[i], all[j])) next = j;
            }
            kept.Add(all[next]);
            i = next;
        }
        return kept;
    }

    private static List<Position> Thin(List<Position> route, int maxPoints)
    {
        if (route.Count <= maxPoints) return route;
        var thin = new List<Position>(maxPoints);
        for (int n = 0; n < maxPoints; n++) thin.Add(route[(int)MathF.Round((route.Count - 1) * (n + 1f) / maxPoints)]);
        return thin;
    }
}
