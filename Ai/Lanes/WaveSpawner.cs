namespace BotArena.Ai.Lanes;

public sealed class WaveSpawner(MobDirector director)
{
    private sealed record PendingSpawn(MobKind Kind, LaneDef Lane, Position At, TimeSpan Due, WaveGroup Clock, float Offset);

    private readonly List<WaveGroup> groups = [];
    private readonly List<PendingSpawn> pending = [];
    private TimeSpan? nextWave;
    private TimeSpan? lastUpdate;
    private float loggedSpeed = LaneSettings.PaceSpeed;
    private int waves;

    public int Waves => waves;

    public void Update(TimeSpan now, bool anyonePlaying)
    {
        if (!LaneSettings.Enabled || !LaneSettings.WavesEnabled) return;
        if (!anyonePlaying) { nextWave = null; lastUpdate = null; pending.Clear(); return; }
        AdvanceClocks(now);
        SpawnDue(now);
        if (waves > 0 && !LaneSettings.RepeatWaves) return;
        nextWave ??= now + LaneSettings.FirstWave;
        if (now < nextWave) return;
        SpawnNow(now);
        nextWave = now + LaneSettings.WaveInterval;
    }

    public void Advance(TimeSpan now) => AdvanceClocks(now);

    private void AdvanceClocks(TimeSpan now)
    {
        float elapsed = lastUpdate is { } last ? (float)(now - last).TotalSeconds : 0f;
        lastUpdate = now;
        groups.RemoveAll(group => !director.Mobs.Any(mob => mob.Wave == group) && !pending.Any(p => p.Clock == group));
        foreach (WaveGroup group in groups) group.Progress += director.Pace.Speed * elapsed;
        if (LaneSettings.LogSpells && MathF.Abs(director.Pace.Speed - loggedSpeed) > loggedSpeed * 0.1f)
        {
            loggedSpeed = director.Pace.Speed;
            director.Log($"[TEMPO] {director.Pace.Describe()}");
        }
    }

    private MinionRole[] NextComposition()
    {
        if (LaneSettings.CannonEvery <= 0 || (waves + 1) % LaneSettings.CannonEvery != 0) return LaneSettings.Wave;
        int firstCaster = Array.FindIndex(LaneSettings.Wave, role => role == MinionRole.Caster);
        int at = firstCaster < 0 ? LaneSettings.Wave.Length : firstCaster;
        return [.. LaneSettings.Wave[..at], MinionRole.Cannon, .. LaneSettings.Wave[at..]];
    }

    public bool SpawnCannon(Faction team, Position near, TimeSpan now)
    {
        if (!LaneSettings.Enabled || LaneSettings.Lanes.Length == 0) return false;
        LaneDef lane = LaneSettings.Lanes.OrderBy(l => l.RouteOf(team)[0].DistanceTo(near).Centimeters).First();
        int alive = director.Mobs.Count(mob => mob.Lane == lane && mob.Kind.Faction == team && !mob.Kind.Immobile && !mob.Champion);
        int waiting = pending.Count(p => p.Lane == lane && p.Kind.Faction == team);
        if (alive + waiting + 1 > LaneSettings.MaxPerTeam) return false;
        var clock = new WaveGroup { Progress = float.MaxValue / 4 };
        groups.Add(clock);
        director.SpawnMinion(MinionKinds.Of(team, MinionRole.Cannon), lane, lane.RouteOf(team)[0], now, clock, 0);
        return true;
    }

    private void SpawnDue(TimeSpan now)
    {
        if (pending.Count == 0) return;
        foreach (PendingSpawn spawn in pending.Where(p => p.Due <= now).OrderBy(p => p.Due).ToArray())
        {
            pending.Remove(spawn);
            director.SpawnMinion(spawn.Kind, spawn.Lane, spawn.At, now, spawn.Clock, spawn.Offset);
        }
    }

    public int SpawnNow(TimeSpan now)
    {
        int planned = 0;
        if (!LaneSettings.Enabled || !LaneSettings.WavesEnabled) return 0;
        MinionRole[] roles = NextComposition();
        foreach (LaneDef lane in LaneSettings.Lanes)
            foreach (Faction team in (Faction[])[Faction.Blue, Faction.Red])
            {
                int alive = director.Mobs.Count(mob => mob.Lane == lane && mob.Kind.Faction == team && !mob.Kind.Immobile && !mob.Champion);
                int waiting = pending.Count(p => p.Lane == lane && p.Kind.Faction == team);
                if (alive + waiting + roles.Length > LaneSettings.MaxPerTeam) continue;
                var clock = new WaveGroup();
                groups.Add(clock);
                Position spawn = lane.RouteOf(team)[0];
                TimeSpan delay = TimeSpan.Zero;
                for (int i = 0; i < roles.Length; i++)
                {
                    if (i > 0) delay += roles[i] == roles[i - 1] ? LaneSettings.WaveStepGap : LaneSettings.WaveGroupGap;
                    float offset = -(float)delay.TotalSeconds * director.Pace.Speed;
                    pending.Add(new PendingSpawn(MinionKinds.Of(team, roles[i]), lane, spawn, now + delay, clock, offset));
                    planned++;
                }
            }
        if (planned > 0) waves++;
        SpawnDue(now);
        return planned;
    }
}
