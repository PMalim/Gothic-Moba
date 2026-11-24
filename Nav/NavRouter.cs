namespace BotArena.Nav;

public sealed class NavRouter(NavGrid grid)
{
    private const float Diagonal = 1.41421356f;

    public IReadOnlyList<Position>? Route(Position from, Position to, int maxPoints = 40)
    {
        if (grid.Nearest(from.X, from.Z) is not { } start || grid.Nearest(to.X, to.Z) is not { } goal) return null;
        List<(int I, int J)>? cells = Search(start, goal, out List<float>? costs);
        if (cells is null || costs is null) return null;
        List<int> keep = Smooth(cells, costs);
        var result = new List<Position>(keep.Count);
        foreach (int index in keep) result.Add(grid.Center(cells[index].I, cells[index].J));
        if (result.Count > 1 && grid.CellOf(from.X, from.Z) == start) result.RemoveAt(0);
        (int ti, int tj) = grid.CellOf(to.X, to.Z);
        if (grid.Walkable(ti, tj)) result[^1] = new Position(to.X, grid.Ground[grid.Index(ti, tj)], to.Z);
        return Thin(result, maxPoints);
    }

    private List<(int I, int J)>? Search((int I, int J) start, (int I, int J) goal, out List<float>? costs)
    {
        costs = null;
        int w = grid.Width, total = w * grid.Height;
        var g = new float[total];
        var parent = new int[total];
        var closed = new bool[total];
        Array.Fill(g, float.MaxValue);
        int s = grid.Index(start.I, start.J), t = grid.Index(goal.I, goal.J);
        g[s] = 0; parent[s] = -1;
        var open = new PriorityQueue<int, float>();
        open.Enqueue(s, Heuristic(start.I, start.J, goal.I, goal.J));
        while (open.TryDequeue(out int current, out _))
        {
            if (closed[current]) continue;
            closed[current] = true;
            if (current == t) break;
            int ci = current % w, cj = current / w;
            byte mask = grid.Mask[current];
            for (int d = 0; d < 8; d++)
            {
                if ((mask & (1 << d)) == 0) continue;
                int ni = ci + NavGrid.Dx[d], nj = cj + NavGrid.Dz[d];
                if (!grid.Walkable(ni, nj)) continue;
                int n = grid.Index(ni, nj);
                if (closed[n]) continue;
                if (d % 2 == 1 && (!grid.Walkable(ci + NavGrid.Dx[d], cj) || !grid.Walkable(ci, cj + NavGrid.Dz[d]))) continue;
                float step = (d % 2 == 1 ? Diagonal : 1f) * NavCost.Multiplier(grid.Cost[n]);
                float next = g[current] + step;
                if (next >= g[n]) continue;
                g[n] = next; parent[n] = current;
                open.Enqueue(n, next + Heuristic(ni, nj, goal.I, goal.J));
            }
        }
        if (g[t] == float.MaxValue) return null;
        var path = new List<(int, int)>();
        var accumulated = new List<float>();
        for (int at = t; at >= 0; at = parent[at]) { path.Add((at % w, at / w)); accumulated.Add(g[at]); }
        path.Reverse(); accumulated.Reverse();
        costs = accumulated;
        return path;
    }

    private static float Heuristic(int i, int j, int gi, int gj)
    {
        int dx = Math.Abs(i - gi), dz = Math.Abs(j - gj);
        return (Math.Max(dx, dz) + (Diagonal - 1f) * Math.Min(dx, dz)) * 0.55f;
    }

    private List<int> Smooth(List<(int I, int J)> cells, List<float> costs)
    {
        var keep = new List<int> { 0 };
        int anchor = 0;
        while (anchor < cells.Count - 1)
        {
            int best = anchor + 1;
            for (int far = Math.Min(cells.Count - 1, anchor + 60); far > anchor + 1; far--)
            {
                float line = LineCost(cells[anchor], cells[far]);
                if (line >= 0 && line <= costs[far] - costs[anchor] + 0.01f) { best = far; break; }
            }
            keep.Add(best);
            anchor = best;
        }
        return keep;
    }

    private float LineCost((int I, int J) a, (int I, int J) b)
    {
        float ax = grid.CenterX(a.I), az = grid.CenterZ(a.J), bx = grid.CenterX(b.I), bz = grid.CenterZ(b.J);
        float length = MathF.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        int steps = Math.Max(1, (int)(length / (grid.Cell * 0.5f)));
        float sum = 0f, per = length / steps / grid.Cell, previous = grid.Ground[grid.Index(a.I, a.J)];
        for (int n = 1; n <= steps; n++)
        {
            float t = n / (float)steps;
            (int i, int j) = grid.CellOf(ax + (bx - ax) * t, az + (bz - az) * t);
            if (!grid.Walkable(i, j)) return -1f;
            int index = grid.Index(i, j);
            if (grid.Mask[index] != 255 && n < steps && (i, j) != a) return -1f;
            if (MathF.Abs(grid.Ground[index] - previous) > 70f) return -1f;
            previous = grid.Ground[index];
            sum += per * NavCost.Multiplier(grid.Cost[index]);
        }
        return sum;
    }

    private static List<Position> Thin(List<Position> points, int max)
    {
        if (points.Count <= max) return points;
        var thinned = new List<Position>(max);
        for (int n = 0; n < max; n++) thinned.Add(points[(int)MathF.Round(n * (points.Count - 1) / (float)(max - 1))]);
        return thinned;
    }
}
