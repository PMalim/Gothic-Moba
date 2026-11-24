namespace BotArena.Ai.Lanes;

public sealed record TurretDef(Faction Team, Position At, float Angle, StructureKind Kind = StructureKind.Turret, int Tier = 1);

public sealed class LaneDef
{
    private Position[] blue;
    private Position[] red;
    private float[] blueDistance;
    private float[] redDistance;
    private readonly TurretDef[] turrets;

    public LaneDef(string name, Position[] points, TurretDef[]? turrets = null)
    {
        if (points.Length < 2) throw new ArgumentException("Linia potrzebuje co najmniej dwóch punktów (obu baz).", nameof(points));
        Name = name;
        this.turrets = turrets ?? [];
        blue = points;
        red = [.. points.Reverse()];
        blueDistance = Cumulative(blue);
        redDistance = Cumulative(red);
    }

    public void SetPoints(Position[] points)
    {
        if (points.Length < 2) throw new ArgumentException("Linia potrzebuje co najmniej dwóch punktów (obu baz).", nameof(points));
        Position[] newRed = [.. points.Reverse()];
        (blueDistance, redDistance) = (Cumulative(points), Cumulative(newRed));
        (blue, red) = (points, newRed);
    }

    public void SetTurret(int index, TurretDef def) => turrets[index] = def;

    public string Name { get; }
    public IReadOnlyList<TurretDef> Turrets => turrets;
    public float Length => blueDistance[^1];

    public static bool IsTeam(Faction faction) => faction is Faction.Blue or Faction.Red;
    public static Faction Enemy(Faction team) => team == Faction.Blue ? Faction.Red : Faction.Blue;

    public Position[] RouteOf(Faction team) => team == Faction.Blue ? blue : red;

    public Position SpawnOf(Faction team) => RouteOf(team)[0];

    public float ProgressOf(Faction team, Position at, int next)
    {
        Position[] route = RouteOf(team);
        float[] distance = team == Faction.Blue ? blueDistance : redDistance;
        int i = Math.Clamp(next - 1, 0, route.Length - 2);
        Position a = route[i], b = route[i + 1];
        float dx = b.X - a.X, dz = b.Z - a.Z, length = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        float along = ((at.X - a.X) * dx + (at.Z - a.Z) * dz) / length;
        along = i == 0 ? MathF.Min(along, length) : Math.Clamp(along, 0f, length);
        return distance[i] + along;
    }

    public float DistanceAt(Faction team, int index) => (team == Faction.Blue ? blueDistance : redDistance)[Math.Clamp(index, 0, blue.Length - 1)];

    public Position Waypoint(Faction team, int index, float lateral)
    {
        Position[] route = RouteOf(team);
        Position p = route[index];
        if (lateral == 0f) return p;
        Position from = route[Math.Max(0, index - 1)], to = route[Math.Min(route.Length - 1, index + 1)];
        float dx = to.X - from.X, dz = to.Z - from.Z, length = MathF.Sqrt(dx * dx + dz * dz);
        if (length < 1f) return p;
        return new Position(p.X - dz / length * lateral, p.Y, p.Z + dx / length * lateral);
    }

    public static float Project(Position[] route, Position p)
    {
        float best = float.MaxValue, along = 0f, walked = 0f;
        for (int i = 0; i + 1 < route.Length; i++)
        {
            Position a = route[i], b = route[i + 1];
            float sx = b.X - a.X, sz = b.Z - a.Z, length = MathF.Sqrt(sx * sx + sz * sz);
            if (length < 1f) continue;
            float t = Math.Clamp(((p.X - a.X) * sx + (p.Z - a.Z) * sz) / (length * length), 0f, 1f);
            float px = a.X + sx * t, pz = a.Z + sz * t;
            float distance = MathF.Sqrt((p.X - px) * (p.X - px) + (p.Z - pz) * (p.Z - pz));
            if (distance < best) { best = distance; along = walked + t * length; }
            walked += length;
        }
        return along;
    }

    private static float[] Cumulative(Position[] route)
    {
        var total = new float[route.Length];
        for (int i = 1; i < route.Length; i++)
        {
            float dx = route[i].X - route[i - 1].X, dz = route[i].Z - route[i - 1].Z;
            total[i] = total[i - 1] + MathF.Sqrt(dx * dx + dz * dz);
        }
        return total;
    }
}
