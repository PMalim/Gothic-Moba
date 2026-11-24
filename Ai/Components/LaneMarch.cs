namespace BotArena.Ai.Components;

public sealed class LaneState
{
    public int Next = 1;
    public int Stuck;
    public TimeSpan LastVirtual;
    public TimeSpan SampleAt;
    public Position SamplePosition;
    public bool Holding;
    public List<Position> Leg = [];
    public int LegFor = -1;
    public float Lift;
}

public sealed class LaneMarch : MobComponent
{
    private const int GiveUpPointAfter = 3;
    private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CombatQuiet = TimeSpan.FromSeconds(3);
    public override string Name => "marsz linią";

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Lane, new Sequence<MobContext>("marsz",
            new Condition<MobContext>("ma linię?", c => c.Mob.Lane is not null),
            new Do<MobContext>("wybiera punkt", Pick),
            new Selector<MobContext>("tempo",
                new Sequence<MobContext>("przed zegarem fali",
                    new Condition<MobContext>("przed zegarem fali?", AheadOfWave),
                    new Selector<MobContext>("zwalnia albo czeka",
                        new Sequence<MobContext>("zwalnia zamiast stać",
                            new Condition<MobContext>("krokiem, nie postojem?", _ => LaneSettings.HoldWalks),
                            new NpcActionLeaf("idzie krokiem (zegar fali)", c => March(c, walk: true), TimeSpan.FromSeconds(42), finished: Reached)),
                        new Do<MobContext>("czeka na falę", c =>
                        {
                            c.Get<LaneState>().SampleAt = default;
                            float mine = c.Mob.Lane!.ProgressOf(c.Kind.Faction, c.Position, c.Get<LaneState>().Next);
                            c.Mob.Note = $"czeka na falę: zegar {c.Mob.Wave!.Progress + c.Mob.WaveOffset:0}, ja {mine:0}, tempo {c.Director.Pace.Speed:0}";
                            return NodeStatus.Running;
                        }))),
                new NpcActionLeaf("idzie linią", c => March(c, walk: false), TimeSpan.FromSeconds(27), finished: Reached))));
    }

    private static int March(MobContext c, bool walk)
    {
        Position point = NextPoint(c);
        c.Mob.SetFocus(point, FocusKind.Wander);
        if (walk) c.Mob.Note = $"zwalnia pod zegar fali: zegar {c.Mob.Wave!.Progress + c.Mob.WaveOffset:0}";
        if (Flat(c.Position, point) > FarLeg) return c.Commands.PathTo(c.Npc, null, point, new Distance(LaneSettings.WaypointReach), walk, TimeSpan.FromSeconds(walk ? 40 : 25));
        return c.Commands.MoveTo(c.Npc, point, new Distance(LaneSettings.WaypointReach), walk, TimeSpan.FromSeconds(walk ? 40 : 25));
    }

    private const float FarLeg = 1200f;

    private static void Reached(MobContext c, bool reached)
    {
        LaneState state = c.Get<LaneState>();
        if (reached) { state.Next++; state.Stuck = 0; }
        else if (++state.Stuck >= GiveUpPointAfter) { state.Next++; state.Stuck = 0; }
    }

    public static bool CaughtUp(Mob mob, Position position)
    {
        if (mob.Wave is not { } wave || mob.Lane is not { } lane) return true;
        LaneState state = mob.Memory.Get<LaneState>();
        float lead = lane.ProgressOf(mob.Kind.Faction, position, state.Next) - (wave.Progress + mob.WaveOffset);
        if (lead > 0f) return false;
        state.Holding = false;
        return true;
    }

    private static Position NextPoint(MobContext c) => Point(c, c.Get<LaneState>().Next);

    private static Position Point(MobContext c, int index) => c.Mob.Lane!.Waypoint(c.Kind.Faction, index, Lateral(c.Mob));

    public static float Lateral(Mob mob) => (mob.Number % 3 - 1) * LaneSettings.ColumnSpread;

    private static bool AheadOfWave(MobContext c)
    {
        LaneState state = c.Get<LaneState>();
        if (c.Mob.Wave is not { } wave || c.Mob.Lane is not { } lane) { c.Director.Slowed(c.Mob, false); return state.Holding = false; }
        float lead = lane.ProgressOf(c.Kind.Faction, c.Position, state.Next) - (wave.Progress + c.Mob.WaveOffset);
        state.Holding = state.Holding ? lead > 0f : lead > LaneSettings.PaceMargin;
        c.Director.Slowed(c.Mob, state.Holding);
        return state.Holding;
    }

    private static NodeStatus Pick(MobContext c)
    {
        LaneDef lane = c.Mob.Lane!;
        Position[] route = lane.RouteOf(c.Kind.Faction);
        LaneState state = c.Get<LaneState>();
        float along = LaneDef.Project(route, c.Position);
        while (state.Next < route.Length
            && (Flat(c.Position, Point(c, state.Next)) <= LaneSettings.WaypointReach
                || along >= lane.DistanceAt(c.Kind.Faction, state.Next) - LaneSettings.WaypointReach)) state.Next++;
        if (state.Next >= route.Length)
        {
            if (c.Mob.Champion) { c.Mob.Note = "stoi w bazie wroga"; return NodeStatus.Failure; }
            c.Director.Retire(c.Mob, "dotarł do bazy wroga");
            return NodeStatus.Failure;
        }
        Measure(c, state);
        return NodeStatus.Success;
    }

    private static void Measure(MobContext c, LaneState state)
    {
        bool running = c.Mob.CurrentAction is not null && c.Now - c.Mob.LastCombat > CombatQuiet && !state.Holding;
        if (!running) { state.SampleAt = default; return; }
        if (state.SampleAt == default) { state.SampleAt = c.Now; state.SamplePosition = c.Position; return; }
        TimeSpan elapsed = c.Now - state.SampleAt;
        if (elapsed < SampleWindow) return;
        c.Director.Pace.Report(c.Kind.Key, Flat(c.Position, state.SamplePosition) / (float)elapsed.TotalSeconds);
        state.SampleAt = c.Now;
        state.SamplePosition = c.Position;
    }

    public override void Dormant(MobContext c)
    {
        if (c.Mob.Lane is not { } lane) return;
        LaneState state = c.Get<LaneState>();
        TimeSpan previous = state.LastVirtual == default ? c.Now : state.LastVirtual;
        state.LastVirtual = c.Now;
        state.SampleAt = default;
        float step = c.Director.Pace.Speed * (float)Math.Min((c.Now - previous).TotalSeconds, 1.0);
        state.Holding = false;
        c.Director.Slowed(c.Mob, false);
        if (DormantSkirmish.NearestEnemy(c) is { } enemy) { DormantSkirmish.Engage(c, enemy, step); return; }
        c.Mob.Target = null;
        if (step <= 0) return;

        Position[] route = lane.RouteOf(c.Kind.Faction);
        Position at = c.Position;
        while (step > 0 && state.Next < route.Length)
        {
            Position goal = Point(c, state.Next);
            if (state.LegFor != state.Next)
            {
                state.LegFor = state.Next;
                state.Leg = Flat(at, goal) > FarLeg && MobCommands.Navigator?.Invoke(at, goal) is { Count: > 0 } found ? [.. found] : [goal];
                state.Lift = state.Leg.Count > 0 && state.Leg[0].Y != goal.Y ? Math.Clamp(at.Y - state.Leg[0].Y, -50f, 200f) : 0f;
                if (state.Leg.Count == 1 && state.Leg[0].Equals(goal)) state.Lift = 0f;
            }
            if (state.Leg.Count == 0) { state.Next++; continue; }
            Position leg = state.Leg[0];
            Position target = new(leg.X, leg.Y + state.Lift, leg.Z);
            float dx = target.X - at.X, dz = target.Z - at.Z, distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= step) { at = target; step -= distance; state.Leg.RemoveAt(0); }
            else
            {
                float k = step / distance;
                at = new Position(at.X + dx * k, at.Y + (target.Y - at.Y) * k, at.Z + dz * k);
                step = 0;
            }
        }
        c.Npc.Position = at;
        c.Mob.LastPosition = at;
        if (state.Next >= route.Length && !c.Mob.Champion) c.Director.Retire(c.Mob, "dotarł do bazy wroga");
    }

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
