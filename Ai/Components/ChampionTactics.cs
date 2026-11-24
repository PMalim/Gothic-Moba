using BotArena.Heroes;

namespace BotArena.Ai.Components;

public sealed class ChampionPlan
{
    public Position? Anchor;
    public bool Recalling;
    public bool Shopping;
    public int MoveFails;
    public Position StartedAt;
    public TimeSpan WatchAt;
    public Position WatchPos;
    public TimeSpan FailAt;
    public TimeSpan LastServerStep;
    public bool Retreating;
    public bool Defending;
    public Position? DefendAt;
    public bool WavePast;
    public string Mode = "";
    public TimeSpan LastVirtual;
    public bool Moving;
    public bool Advancing;
    public float LastDesired;
    public TimeSpan LastDesiredAt;
    public TimeSpan ChannelUntil;
    public TimeSpan ChannelStarted;
    public int ChannelHealth;
    public Character? Finishing;
}

public sealed class ChampionLaneMemory : IPersistentState
{
    public bool WaveSeen { get; set; }
    public bool LaneLocked { get; set; }
}

public sealed class ChampionTactics : MobComponent
{
    public const float FollowBehind = 350f;
    private const float BaseOffset = 700f;
    private const float TurretMargin = 300f;
    private const float CloseThreat = 650f;
    private const float OpportunityRange = 1400f;
    private const float FinishRange = 1000f;
    private const float TowerAttackRange = 1300f;
    private const float AllyRadius = 1500f;
    private const float DefendRange = 1900f;
    private const float TerritoryBeyondTurret = 1000f;
    private const float ProactiveRange = 2000f;
    private const float DangerRange = 2200f;
    private const float ShopRecallBelow = 0.7f;
    private const float RecallBelow = 0.40f, RecoverTo = 0.95f;
    private const float HoldBehindTurret = 350f;
    private const float DetectRange = 1700f;
    private const float ChaseRange = 2600f;
    private const float AssistRange = 2200f;
    private const float DiveHealth = 0.7f;
    private const float RetreatBelow = 0.55f, RetreatUntil = 0.7f, RetreatThreat = 1800f;
    private const float FarmRange = 700f;
    private const float SafeTowerApproach = 2300f;
    private const float AllyFollowRange = 6000f;
    private static readonly TimeSpan AggressionWindow = TimeSpan.FromSeconds(3);
    private const float AdvanceLead = 250f;
    private const float CommitDistance = 1300f;
    private const float CommitMargin = 400f;
    private const float OffLane = 2500f;
    private const string DuoLane = "Dolna";
    private static readonly TimeSpan LaneLockAfter = TimeSpan.FromSeconds(45);
    private const float StrengthMargin = 1.25f;
    private const float WaveCrowd = 600f;

    public override string Name => "taktyka (czeka na falę, gra bezpiecznie)";

    private sealed class TurretView(Combatant who, bool busy, bool aimsAtMe, float reach, bool guns, bool vulnerable)
    {
        public Combatant Who { get; } = who;
        public float Reach { get; } = reach;
        public bool Guns { get; } = guns;
        public bool Vulnerable { get; } = vulnerable;
        public bool Busy { get; } = busy;
        public bool AimsAtMe { get; } = aimsAtMe;
    }

    private sealed class Scene
    {
        public float? Front;
        public readonly List<TurretView> Turrets = [];
        public readonly List<Combatant> EnemyChampions = [];
        public readonly List<Combatant> EnemyMinions = [];
        public readonly List<Combatant> FriendChampions = [];
        public readonly List<Combatant> FriendPlayers = [];
        public readonly List<Combatant> OwnTurrets = [];
    }

    public override void React(MobContext c)
    {
        AssignLane(c);
        Scene scene = Look(c);
        ChampionPlan plan = Replan(c, scene);
        Combatant? target = Choose(c, scene, plan);
        c.Target = target;
        c.Mob.Target = target?.Character;
        if (target is { } chosen) c.Mob.Quarry = chosen.Character;
    }

    public static (ChampionPlan Plan, Combatant? Target) Decide(MobContext c)
    {
        AssignLane(c);
        Scene scene = Look(c);
        ChampionPlan plan = Replan(c, scene);
        return (plan, Choose(c, scene, plan));
    }

    private static ChampionPlan Replan(MobContext c, Scene scene)
    {
        ChampionPlan plan = c.Get<ChampionPlan>();
        ChampionLaneMemory memory = c.Get<ChampionLaneMemory>();
        float health = c.Health;
        bool safe = scene.EnemyChampions.All(e => Flat(e.Position, c.Position) > DangerRange);
        if (plan.Recalling) { if (health >= RecoverTo && !(plan.Shopping && c.Mob.WantsShop)) plan.Recalling = plan.Shopping = false; }
        else if (health < RecallBelow) plan.Recalling = true;
        else if (c.Mob.WantsShop && health < ShopRecallBelow && safe && !plan.Defending) plan.Recalling = plan.Shopping = true;

        if (c.Mob.Lane is not { } lane) return plan;
        Position[] route = lane.RouteOf(c.Kind.Faction);
        if (scene.Front is not null) memory.WaveSeen = true;
        float idle = BaseOffset;
        if (scene.OwnTurrets.Count > 0)
            idle = MathF.Max(BaseOffset, scene.OwnTurrets.Max(t => Progress(route, t.Position)) - HoldBehindTurret);
        float desired = scene.Front is { } front ? MathF.Max(front - FollowBehind, idle) : idle;
        string followMode = "";
        if (scene.Front is null && AllyFront(c, scene, route) is { } allyAt && allyAt - FollowBehind > desired)
        {
            desired = allyAt - FollowBehind;
            followMode = "idzie za sojuszniczym bohaterem";
        }
        float safeMax = float.MaxValue;
        foreach (TurretView turret in scene.Turrets)
            if (turret.Guns && !turret.Busy) safeMax = MathF.Min(safeMax, Progress(route, turret.Who.Position) - (turret.Reach + TurretMargin));
        desired = MathF.Max(BaseOffset, MathF.Min(desired, safeMax));
        plan.Mode = scene.Front is not null ? "idzie za falą" : followMode.Length > 0 ? followMode : memory.WaveSeen ? "pilnuje linii pod własną wieżą" : "czeka na falę pod własną wieżą";
        if (scene.Front is null && followMode.Length == 0 && Progress(route, c.Position) < desired - 800f) plan.Mode = $"idzie na swoją linię ({lane.Name})";
        float territoryEnd = scene.OwnTurrets.Count > 0 ? scene.OwnTurrets.Max(t => Progress(route, t.Position)) + TerritoryBeyondTurret : BaseOffset + 1500f;
        bool wavePast = scene.Front is { } waveAt && waveAt > territoryEnd;
        Combatant? threatened = ThreatenedTurret(scene);
        plan.WavePast = wavePast;
        plan.Defending = threatened is not null;
        plan.DefendAt = threatened?.Position;
        bool threatNear = scene.EnemyChampions.Any(e => Flat(e.Position, c.Position) <= RetreatThreat);
        bool outnumbered = Outnumbered(c, scene);
        bool outclassed = health < 0.7f && scene.EnemyChampions.Any(e => Flat(e.Position, c.Position) <= RetreatThreat
            && HealthOf(e).Health > c.State.Health * 2 && c.Memory.Recall(e.Character) is { Threat: > 0 });
        if (plan.Retreating) { if ((!threatNear && !outnumbered) || (health >= RetreatUntil && !outnumbered)) plan.Retreating = false; }
        else if ((health < RetreatBelow && threatNear) || outnumbered || outclassed) plan.Retreating = true;
        if (plan.Retreating && health < RetreatBelow) plan.Recalling = true;
        Combatant? finish = Finishable(c, scene, plan, outnumbered);
        plan.Finishing = finish?.Character;
        if (finish is not null)
        {
            plan.Retreating = false;
            plan.Recalling = plan.Shopping = false;
        }
        float along = Progress(route, c.Position);
        float? safeSpot = SafeSpot(scene, route, along);
        bool channel = false;
        if (plan.Recalling) desired = RecallSpot(c, scene, plan, route, along, safeSpot, out channel);
        else
        {
            CancelChannel(c, plan);
            if (finish is not null) plan.Mode = "wygrywa walkę: dobija słabszego wroga";
            else if (plan.Retreating)
            {
                desired = safeSpot is { } spot ? MathF.Max(BaseOffset, spot - 220f) : 150f;
                plan.Mode = outnumbered ? "kilku wrogów naraz: cofa się pod najbliższą własną wieżę" : "cofa się pod najbliższą własną wieżę";
            }
            else if (threatened is { } tower)
            {
                desired = MathF.Max(BaseOffset, Progress(route, tower.Position) - 220f);
                plan.Mode = "broni własnej wieży";
            }
            else if (wavePast && safeSpot is { } back && DangerAhead(c, scene))
            {
                desired = MathF.Max(BaseOffset, back - 220f);
                plan.Mode = "ryzyko przed nią: wycofuje się pod własną wieżę";
            }
            else if (scene.Turrets.Any(t => t.AimsAtMe)) plan.Mode = "wieża celuje we mnie: wycofuje się";
        }
        float lateral = (c.Mob.Number % 3 - 1) * 170f;
        if (c.Now - plan.LastDesiredAt >= TimeSpan.FromMilliseconds(400))
        {
            plan.Advancing = desired > plan.LastDesired + 20f && !plan.Recalling;
            plan.LastDesired = desired;
            plan.LastDesiredAt = c.Now;
        }
        if (plan.Advancing && scene.Front is { } waveFront)
            desired = MathF.Max(desired, MathF.Min(MathF.Min(desired + AdvanceLead, waveFront - 120f), safeMax));
        plan.Anchor = channel ? c.Position : PointAt(route, desired, lateral);
        WatchProgress(c, plan);
        return plan;
    }

    private const float RecallQuiet = 1300f;
    private const float RecallMinionQuiet = 700f;

    private static float? SafeSpot(Scene s, Position[] route, float along)
    {
        float? best = null;
        foreach (Combatant turret in s.OwnTurrets)
        {
            float at = Progress(route, turret.Position);
            if (at <= along + 300f && (best is null || at > best)) best = at;
        }
        return best;
    }

    private static float RecallSpot(MobContext c, Scene s, ChampionPlan plan, Position[] route, float along, float? safeSpot, out bool channel)
    {
        channel = false;
        if (ChampionSkills.AtBase(c))
        {
            CancelChannel(c, plan);
            plan.Mode = plan.Shopping ? "w bazie: zakupy" : "w bazie: leczy się przy fontannie";
            return 150f;
        }
        bool quiet = s.EnemyChampions.All(e => Flat(e.Position, c.Position) > RecallQuiet) && !UnderFire(c.Position, s)
            && s.EnemyMinions.All(m => Flat(m.Position, c.Position) > RecallMinionQuiet);
        float front = s.OwnTurrets.Count > 0 ? s.OwnTurrets.Max(t => Progress(route, t.Position)) : float.MaxValue;
        bool safeGround = plan.Shopping || along <= front + 250f;
        if (plan.ChannelUntil != default)
        {
            bool hurt = c.State.Health < plan.ChannelHealth || (c.Mob.LastHurt is { } hurtAt && hurtAt > plan.ChannelStarted);
            if (hurt || !quiet) { CancelChannel(c, plan); plan.Mode = "powrót do bazy przerwany"; }
            else if (c.Now >= plan.ChannelUntil)
            {
                TeleportHome(c, plan);
                return 150f;
            }
            else
            {
                channel = true;
                plan.Mode = $"powrót do bazy: {(plan.ChannelUntil - c.Now).TotalSeconds:0.0} s";
                return along;
            }
        }
        if (quiet && safeGround)
        {
            plan.ChannelStarted = c.Now;
            plan.ChannelUntil = c.Now + BotArena.Heroes.AbilityService.RecallTime;
            plan.ChannelHealth = c.State.Health;
            c.Director.ChampionAura(c.Mob, BotArena.Heroes.AbilityService.RecallFx, (int)BotArena.Heroes.AbilityService.RecallTime.TotalMilliseconds);
            c.Mob.LastCombat = default;
            channel = true;
            plan.Mode = "powrót do bazy: zaczyna kanał";
            return along;
        }
        if (!safeGround && safeSpot is { } spot)
        {
            plan.Mode = plan.Shopping ? "wraca na zakupy: pod własną wieżę" : "ranny: cofa się pod najbliższą własną wieżę (potem powrót do bazy)";
            return MathF.Max(BaseOffset, spot - 220f);
        }
        plan.Mode = "pod wieżą nie jest spokojnie: cofa się dalej w stronę bazy";
        return 150f;
    }

    private static void CancelChannel(MobContext c, ChampionPlan plan)
    {
        if (plan.ChannelUntil == default) return;
        plan.ChannelUntil = default;
        c.Director.ChampionAura(c.Mob, BotArena.Heroes.AbilityService.RecallFx, 0);
    }

    private static void TeleportHome(MobContext c, ChampionPlan plan)
    {
        plan.ChannelUntil = default;
        Position fountain = LaneSettings.Lanes[0].SpawnOf(c.Kind.Faction);
        float side = (c.Mob.Number % 3 - 1) * 150f;
        var home = new Position(fountain.X + side, fountain.Y + 40f, fountain.Z + side);
        c.Director.ChampionAura(c.Mob, BotArena.Heroes.AbilityService.RecallFx, 0);
        c.Npc.Position = home;
        c.Mob.LastPosition = home;
        c.Director.ChampionEffect(c.Mob, null, 1, BotArena.Heroes.AbilityService.RecallFx);
        plan.MoveFails = 0;
        plan.Moving = false;
        c.Mob.Note = "taktyka: wrócił do bazy (B)";
    }

    private static Combatant? Finishable(MobContext c, Scene s, ChampionPlan plan, bool outnumbered)
    {
        if (outnumbered) return null;
        float myHealth = c.Health;
        int myHp = c.State.Health;
        int myDamage = c.Mob.ChampionDamage ?? c.Kind.Damage ?? 20;
        Combatant? best = null;
        int bestHp = int.MaxValue;
        foreach (Combatant enemy in s.EnemyChampions)
        {
            bool same = ReferenceEquals(plan.Finishing, enemy.Character);
            float distance = Flat(c.Position, enemy.Position);
            if (distance > (same ? ChaseRange : OpportunityRange)) continue;
            (int hp, int max) = HealthOf(enemy);
            if (hp <= 0) continue;
            float ratio = (float)hp / max;
            if (hp >= myHp || ratio >= myHealth + (same ? 0.05f : -0.05f)) continue;
            bool lethal = hp <= myDamage * 3;
            if (!(lethal || ratio <= 0.4f || hp <= myHp * 0.6f)) continue;
            if (myHealth < 0.15f && !lethal) continue;
            if (Allies(c, s, enemy.Position) < EnemiesNear(s, enemy.Position)) continue;
            if ((NearEnemyTurret(enemy.Position, s) || NearEnemyTurret(c.Position, s)) && !(hp <= myDamage * 2 && myHealth >= 0.3f)) continue;
            if (!lethal && s.EnemyMinions.Count(m => Flat(m.Position, c.Position) <= WaveCrowd) >= 4) continue;
            if (hp < bestHp) { bestHp = hp; best = enemy; }
        }
        return best;
    }

    private static bool UnderOwnTurret(Position at, Scene s)
    {
        foreach (Combatant turret in s.OwnTurrets)
            if (Flat(at, turret.Position) <= LaneSettings.TurretRange.Centimeters) return true;
        return false;
    }

    private static void WatchProgress(MobContext c, ChampionPlan plan)
    {
        if (plan.Anchor is not { } anchor || Flat(c.Position, anchor) <= 400f) { plan.WatchAt = default; return; }
        if (plan.WatchAt == default || c.Now - plan.WatchAt > TimeSpan.FromSeconds(10)) { plan.WatchAt = c.Now; plan.WatchPos = c.Position; return; }
        if (c.Now - plan.WatchAt < TimeSpan.FromSeconds(3)) return;
        if (Flat(c.Position, plan.WatchPos) < 150f && c.Now - c.Mob.LastCombat > TimeSpan.FromSeconds(3) && c.Now >= c.Mob.StunnedUntil)
        {
            plan.MoveFails = Math.Max(plan.MoveFails, 2);
            plan.FailAt = c.Now;
        }
        plan.WatchAt = c.Now;
        plan.WatchPos = c.Position;
    }

    private static void AssignLane(MobContext c)
    {
        if (c.Mob.Lane is null) return;
        ChampionLaneMemory memory = c.Get<ChampionLaneMemory>();
        if (memory.LaneLocked) return;
        if (LaneSettings.Lanes.Length < 2 || c.Director.Started == default || c.Now - c.Director.Started >= LaneLockAfter) { memory.LaneLocked = true; return; }
        if (LanePlan(c, c.Kind.Faction).TryGetValue(c.Mob, out LaneDef? lane) && !ReferenceEquals(lane, c.Mob.Lane))
        {
            c.Mob.Lane = lane;
            c.Mob.Note = $"taktyka: wybiera linię {lane.Name}";
        }
    }

    private static Dictionary<Mob, LaneDef> LanePlan(MobContext c, Faction team)
    {
        LaneDef[] lanes = LaneSettings.Lanes;
        int[] taken = new int[lanes.Length];
        bool[] enemyHuman = new bool[lanes.Length];
        Faction foe = team == Faction.Blue ? Faction.Red : Faction.Blue;
        foreach (Combatant human in c.Players)
        {
            if (human.Character is not { IsValid: true, IsDead: false }) continue;
            bool enemy = Teams.IsEnemy(team, human.Character);
            int at = HumanLane(lanes, enemy ? foe : team, human.Position);
            if (at < 0) continue;
            if (enemy) enemyHuman[at] = true;
            else taken[at]++;
        }
        var plan = new Dictionary<Mob, LaneDef>();
        var free = new List<(Mob Bot, string Role)>();
        foreach (Mob bot in c.Director.Mobs.Where(m => m.Champion && m.Kind.Faction == team && m.Lane is not null).OrderBy(m => m.Number))
        {
            if (bot.Memory.Get<ChampionLaneMemory>().LaneLocked)
            {
                plan[bot] = bot.Lane!;
                int index = Array.IndexOf(lanes, bot.Lane);
                if (index >= 0) taken[index]++;
            }
            else free.Add((bot, bot.Behaviour.Get<ChampionSkills>()?.Hero.Role ?? ""));
        }
        while (free.Count > 0)
        {
            int bestBot = 0, bestLane = 0;
            float bestCost = float.MaxValue;
            for (int b = 0; b < free.Count; b++)
                for (int i = 0; i < lanes.Length; i++)
                {
                    float cost = taken[i] * 10f + Preference(free[b].Role, lanes[i].Name, i);
                    if (taken[i] == 1 && lanes[i].Name == DuoLane) cost -= 4f;
                    if (taken[i] == 0 && enemyHuman[i]) cost -= 4f;
                    if (cost < bestCost) { bestCost = cost; bestBot = b; bestLane = i; }
                }
            plan[free[bestBot].Bot] = lanes[bestLane];
            taken[bestLane]++;
            free.RemoveAt(bestBot);
        }
        return plan;
    }

    private static float Preference(string role, string lane, int index)
    {
        string[] order = role switch
        {
            "Strzelec" => ["Dolna", "Środek", "Górna"],
            "Wsparcie" => ["Dolna", "Górna", "Środek"],
            "Mag" or "Zabójca" => ["Środek", "Górna", "Dolna"],
            _ => ["Górna", "Środek", "Dolna"],
        };
        int at = Array.IndexOf(order, lane);
        if (at < 0) return 3f + index;
        return role == "Wsparcie" && at == 0 ? 0.5f : at;
    }

    private static int HumanLane(LaneDef[] lanes, Faction side, Position at)
    {
        if (Flat(at, lanes[0].SpawnOf(side)) < CommitDistance) return -1;
        int best = -1;
        float bestDistance = float.MaxValue, second = float.MaxValue;
        for (int i = 0; i < lanes.Length; i++)
        {
            float distance = DistanceToRoute(lanes[i].RouteOf(side), at);
            if (distance < bestDistance) { second = bestDistance; bestDistance = distance; best = i; }
            else if (distance < second) second = distance;
        }
        return bestDistance > OffLane || second - bestDistance < CommitMargin ? -1 : best;
    }

    private static float DistanceToRoute(Position[] route, Position p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < route.Length; i++)
        {
            Position a = route[i], b = route[i + 1];
            float sx = b.X - a.X, sz = b.Z - a.Z, length2 = sx * sx + sz * sz;
            float t = length2 < 1f ? 0f : Math.Clamp(((p.X - a.X) * sx + (p.Z - a.Z) * sz) / length2, 0f, 1f);
            float dx = p.X - (a.X + sx * t), dz = p.Z - (a.Z + sz * t);
            best = MathF.Min(best, MathF.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    private static Scene Look(MobContext c)
    {
        var scene = new Scene();
        Faction team = c.Kind.Faction;
        LaneDef? lane = c.Mob.Lane;
        Position[]? route = lane?.RouteOf(team);
        foreach (Combatant other in c.Mobs)
        {
            if (other.Mob is not { } mob || ReferenceEquals(mob, c.Mob) || other.Character is not { IsValid: true, IsDead: false }) continue;
            if (mob.Kind.Faction == team)
            {
                if (mob.Champion) scene.FriendChampions.Add(other);
                else if (mob.Kind.Structure == StructureKind.Turret) { if (mob.Tier == 0 || mob.Lane == lane) scene.OwnTurrets.Add(other); }
                else if (route is not null && mob.Lane == lane && !mob.Kind.Immobile)
                {
                    float progress = Progress(route, other.Position);
                    scene.Front = scene.Front is { } known ? MathF.Max(known, progress) : progress;
                }
            }
            else if (Factions.Hostile(team, mob.Kind.Faction))
            {
                if (mob.Kind.Immobile && lane is not null && mob.Lane is { } theirs && !ReferenceEquals(theirs, lane)
                    && (mob.Kind.Structure == StructureKind.Inhibitor || (mob.Kind.Structure == StructureKind.Turret && mob.Tier > 0))) continue;
                if (mob.Kind.Immobile)
                {
                    bool guns = mob.Kind.Structure is StructureKind.Turret or StructureKind.Dragon;
                    bool busy = !guns || mob.Target switch
                    {
                        Npc npc => c.Director.MobOf(npc) is { Champion: false, Lane: not null } m && m.Kind.Faction == team,
                        Player p => !Teams.IsEnemy(team, p),
                        _ => false,
                    };
                    float reach = mob.Kind.Structure == StructureKind.Dragon ? LaneSettings.DragonRange.Centimeters : LaneSettings.TurretSight.Centimeters;
                    scene.Turrets.Add(new TurretView(other, busy, ReferenceEquals(mob.Target, c.Npc), reach, guns, !c.Director.IsProtected(mob)));
                }
                else if (mob.Champion) scene.EnemyChampions.Add(other);
                else if (mob.Lane is not null) scene.EnemyMinions.Add(other);
            }
        }
        foreach (Combatant player in c.Players)
        {
            if (Teams.IsEnemy(team, player.Character)) { if (!Teams.Untargetable(player.Character)) scene.EnemyChampions.Add(player); }
            else scene.FriendPlayers.Add(player);
        }
        return scene;
    }

    private static Combatant? ThreatenedTurret(Scene s)
    {
        foreach (Combatant tower in s.OwnTurrets)
        {
            int champions = s.EnemyChampions.Count(e => Flat(e.Position, tower.Position) <= DefendRange);
            int minions = s.EnemyMinions.Count(e => Flat(e.Position, tower.Position) <= DefendRange);
            if (champions > 0 || minions >= 2) return tower;
        }
        return null;
    }

    private static bool DangerAhead(MobContext c, Scene s)
    {
        int enemies = s.EnemyChampions.Count(e => Flat(e.Position, c.Position) <= DangerRange);
        if (enemies == 0) return false;
        return enemies > Allies(c, s, c.Position) || c.Health < 0.6f;
    }

    private static Combatant? Choose(MobContext c, Scene s, ChampionPlan plan)
    {
        if (plan.Finishing is { } prey)
            foreach (Combatant enemy in s.EnemyChampions)
                if (ReferenceEquals(enemy.Character, prey)) return enemy;
        if (plan.Recalling || plan.Retreating)
        {
            if (plan.ChannelUntil != default || !UnderOwnTurret(c.Position, s)) return null;
            Combatant? diver = null;
            float closest = CloseThreat;
            foreach (Combatant enemy in s.EnemyChampions)
            {
                float distance = Flat(c.Position, enemy.Position);
                if (distance <= closest && UnderOwnTurret(enemy.Position, s)) { closest = distance; diver = enemy; }
            }
            return diver;
        }
        float myHealth = c.Health;
        int myHp = c.State.Health;
        int myDamage = c.Mob.ChampionDamage ?? c.Kind.Damage ?? 20;
        Position me = c.Position;

        if (plan is { Defending: true, DefendAt: { } tower })
        {
            Combatant? defended = null;
            float best = float.MaxValue;
            foreach (Combatant enemy in s.EnemyChampions)
            {
                float fromTower = Flat(enemy.Position, tower);
                if (fromTower > DefendRange || Allies(c, s, enemy.Position) + 1 < EnemiesNear(s, enemy.Position)) continue;
                if (fromTower < best) { best = fromTower; defended = enemy; }
            }
            if (defended is null)
                foreach (Combatant enemy in s.EnemyMinions)
                {
                    float fromTower = Flat(enemy.Position, tower);
                    if (fromTower <= DefendRange && fromTower < best) { best = fromTower; defended = enemy; }
                }
            if (defended is not null) return defended;
        }

        Combatant? champion = null;
        float championScore = float.MaxValue;
        bool meUnderTower = NearEnemyTurret(me, s);
        foreach (Combatant enemy in s.EnemyChampions)
        {
            float distance = Flat(me, enemy.Position);
            (int hp, int max) = HealthOf(enemy);
            float ratio = (float)hp / max;
            int allies = Allies(c, s, enemy.Position), foes = EnemiesNear(s, enemy.Position);
            bool known = c.Memory.Recall(enemy.Character) is { Threat: > 0, Visible: true };
            bool inHisWave = s.EnemyMinions.Count(m => Flat(m.Position, enemy.Position) <= WaveCrowd) >= 3;
            bool stronger = hp <= myHp * StrengthMargin;
            bool focused = distance <= DetectRange && myHealth >= 0.4f && allies >= foes && AllyHits(c, enemy);
            bool close = distance <= CloseThreat;
            bool hurtMe = distance <= OpportunityRange && myHealth >= 0.5f && known && !inHisWave;
            bool kill = distance <= OpportunityRange && myHealth >= 0.5f && (hp <= myDamage * 4 || ratio <= 0.3f) && allies >= foes;
            bool detect = distance <= DetectRange && myHealth >= 0.6f && allies >= foes && ratio <= myHealth + 0.35f && stronger && !inHisWave;
            bool chase = ReferenceEquals(c.Mob.Quarry, enemy.Character) && distance <= ChaseRange && myHealth >= 0.5f && ratio < myHealth + 0.1f && stronger && allies >= foes && !inHisWave;
            bool assist = IsAssist(c, enemy, distance) && myHealth >= 0.5f && allies >= foes;
            bool underTower = NearEnemyTurret(enemy.Position, s);
            bool dive = underTower && distance <= OpportunityRange && myHealth >= DiveHealth && allies >= foes && (hp <= myDamage * 3 || ratio <= 0.25f);
            if ((underTower || meUnderTower) && !dive) continue;
            if (!(close || hurtMe || kill || detect || chase || assist || focused || dive)) continue;
            float score = distance + ratio * 500f - (known ? 300f : 0f) - (assist ? 200f : 0f) - (focused ? 250f : 0f)
                - (hp <= myDamage * 3 ? 300f : 0f) - (ReferenceEquals(c.Mob.Quarry, enemy.Character) ? 150f : 0f);
            if (score >= championScore) continue;
            champion = enemy;
            championScore = score;
        }
        if (champion is not null) return champion;

        Combatant? minion = null;
        float bestScore = float.MaxValue;
        foreach (Combatant enemy in s.EnemyMinions)
        {
            if (UnderFire(enemy.Position, s)) continue;
            float distance = Flat(me, enemy.Position);
            bool attacksMe = distance <= 380f && ReferenceEquals(enemy.Mob!.Target, c.Npc);
            bool finish = distance <= FinishRange && enemy.Mob!.LastHealth > 0 && enemy.Mob.LastHealth <= myDamage * 2;
            bool farm = !plan.WavePast && s.Front is not null && distance <= FarmRange;
            if (!attacksMe && !finish && !farm) continue;
            float score = (attacksMe ? 0f : finish ? 10_000f : 20_000f) + enemy.Mob!.LastHealth * 10f + distance;
            if (score < bestScore) { bestScore = score; minion = enemy; }
        }
        if (minion is not null) return minion;

        if (plan.WavePast && myHealth >= 0.6f)
        {
            Combatant? safeMinion = null;
            float nearest = float.MaxValue;
            foreach (Combatant enemy in s.EnemyMinions)
            {
                float distance = Flat(me, enemy.Position);
                if (distance > ProactiveRange || UnderFire(enemy.Position, s) || EnemiesNear(s, enemy.Position) > 0 || distance >= nearest) continue;
                nearest = distance;
                safeMinion = enemy;
            }
            if (safeMinion is not null) return safeMinion;
            if (myHealth >= 0.7f)
                foreach (Combatant enemy in s.EnemyChampions)
                {
                    (int hp, int max) = HealthOf(enemy);
                    if (Flat(me, enemy.Position) > OpportunityRange || NearEnemyTurret(enemy.Position, s) || NearEnemyTurret(me, s) || EnemiesNear(s, enemy.Position) != 1) continue;
                    if ((float)hp / max < myHealth - 0.1f) return enemy;
                }
        }

        if (s.Turrets.Any(t => t.AimsAtMe)) return null;
        bool calm = myHealth >= 0.6f && s.EnemyChampions.All(e => Flat(e.Position, me) > DangerRange) && s.EnemyMinions.All(m => Flat(m.Position, me) > FarmRange);
        float towerReach = calm ? SafeTowerApproach : TowerAttackRange;
        foreach (TurretView turret in s.Turrets)
            if (turret.Busy && turret.Vulnerable && Flat(me, turret.Who.Position) <= towerReach) return turret.Who;
        return null;
    }

    private static bool IsAssist(MobContext c, Combatant enemy, float distance)
    {
        if (distance > AssistRange) return false;
        foreach ((Character attacker, Character victim, TimeSpan _) in c.Director.RecentAggression(c.Now, AggressionWindow))
            if (ReferenceEquals(attacker, enemy.Character) && !ReferenceEquals(victim, c.Npc) && IsFriendChampion(c, victim)) return true;
        return false;
    }

    private static bool AllyHits(MobContext c, Combatant enemy)
    {
        foreach (Combatant other in c.Mobs)
            if (other.Mob is { Champion: true } mate && !ReferenceEquals(mate, c.Mob) && mate.Kind.Faction == c.Kind.Faction && ReferenceEquals(mate.Target, enemy.Character))
                return true;
        foreach ((Character attacker, Character victim, TimeSpan _) in c.Director.RecentAggression(c.Now, AggressionWindow))
            if (ReferenceEquals(victim, enemy.Character) && !ReferenceEquals(attacker, c.Npc) && IsFriendChampion(c, attacker)) return true;
        return false;
    }

    private static bool IsFriendChampion(MobContext c, Character who) => who switch
    {
        Player player => !Teams.IsEnemy(c.Kind.Faction, player),
        _ => c.Director.MobOf(who) is { Champion: true } mob && mob.Kind.Faction == c.Kind.Faction,
    };

    private static bool Outnumbered(MobContext c, Scene s)
    {
        int attackers = 0;
        foreach (Combatant enemy in s.EnemyChampions)
        {
            if (Flat(enemy.Position, c.Position) > RetreatThreat) continue;
            bool hitsMe = ReferenceEquals(enemy.Mob?.Target, c.Npc);
            if (!hitsMe)
                foreach ((Character attacker, Character victim, TimeSpan _) in c.Director.RecentAggression(c.Now, AggressionWindow))
                    if (ReferenceEquals(attacker, enemy.Character) && ReferenceEquals(victim, c.Npc)) { hitsMe = true; break; }
            if (hitsMe) attackers++;
        }
        return attackers >= 2 && attackers > Allies(c, s, c.Position);
    }

    private static float? AllyFront(MobContext c, Scene s, Position[] route)
    {
        float? best = null;
        foreach (Combatant ally in s.FriendChampions.Concat(s.FriendPlayers))
        {
            if (Flat(ally.Position, c.Position) > AllyFollowRange) continue;
            float progress = Progress(route, ally.Position);
            best = best is { } known ? MathF.Max(known, progress) : progress;
        }
        return best;
    }

    private static bool UnderFire(Position at, Scene s)
    {
        foreach (TurretView turret in s.Turrets)
            if (turret.Guns && !turret.Busy && Flat(at, turret.Who.Position) <= turret.Reach + 100f) return true;
        return false;
    }

    private static bool NearEnemyTurret(Position at, Scene s)
    {
        foreach (TurretView turret in s.Turrets)
            if (turret.Guns && Flat(at, turret.Who.Position) <= turret.Reach + 100f) return true;
        return false;
    }

    private static (int Health, int Max) HealthOf(Combatant who) => who.Character switch
    {
        Player player => (player.Health, Math.Max(1, player.MaxHealth)),
        _ => (who.Mob?.LastHealth ?? 0, Math.Max(1, who.Mob?.LastMaxHealth ?? 1)),
    };

    private static int Allies(MobContext c, Scene s, Position at)
    {
        int count = 1;
        foreach (Combatant friend in s.FriendChampions) if (Flat(friend.Position, at) <= AllyRadius) count++;
        foreach (Combatant player in c.Players)
            if (!Teams.IsEnemy(c.Kind.Faction, player.Character) && Flat(player.Position, at) <= AllyRadius) count++;
        return count;
    }

    private static int EnemiesNear(Scene s, Position at) => s.EnemyChampions.Count(enemy => Flat(enemy.Position, at) <= AllyRadius);

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public static float Progress(Position[] route, Position p) => LaneDef.Project(route, p);

    public static Position PointAt(Position[] route, float progress, float lateral)
    {
        float remaining = MathF.Max(0f, progress);
        for (int i = 0; i + 1 < route.Length; i++)
        {
            Position a = route[i], b = route[i + 1];
            float sx = b.X - a.X, sz = b.Z - a.Z, length = MathF.Sqrt(sx * sx + sz * sz);
            if (length < 1f) continue;
            if (remaining <= length || i + 2 == route.Length)
            {
                float t = Math.Clamp(remaining / length, 0f, 1f);
                return new Position(a.X + sx * t - sz / length * lateral, a.Y + (b.Y - a.Y) * t, a.Z + sz * t + sx / length * lateral);
            }
            remaining -= length;
        }
        return route[^1];
    }
}

public sealed class ChampionStance : MobComponent
{
    private const float Arrived = 140f, Restart = 320f;
    private const float WalkWithin = 600f;
    private const float FarRoute = 1500f;
    public override string Name => "postój i dojście";

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Lane, new Sequence<MobContext>("postój",
            new Condition<MobContext>("ma miejsce?", c => c.Get<ChampionPlan>().Anchor is not null),
            new Selector<MobContext>("idzie albo stoi",
                new Sequence<MobContext>("dojście",
                    new Condition<MobContext>("daleko od miejsca?", c => Flat(c) > (c.Get<ChampionPlan>().Moving ? Arrived : Restart)),
                    new Selector<MobContext>("jak idzie",
                        new Sequence<MobContext>("krok serwera",
                            new Condition<MobContext>("klient zawodzi?", c => c.Get<ChampionPlan>() is { MoveFails: >= 2 } p && c.Now - p.FailAt < TimeSpan.FromSeconds(15)),
                            new Do<MobContext>("idzie krokami serwera", c =>
                            {
                                ChampionPlan plan = c.Get<ChampionPlan>();
                                TimeSpan previous = plan.LastServerStep == default ? c.Now - TimeSpan.FromMilliseconds(500) : plan.LastServerStep;
                                plan.LastServerStep = c.Now;
                                plan.Moving = true;
                                float step = RunSpeed(c) * (float)Math.Min((c.Now - previous).TotalSeconds, 1.0);
                                DormantWalk(c, plan.Anchor!.Value, step);
                                c.Mob.Note = "taktyka: serwer prowadzi bota (klient go zatrzymywał)";
                                return NodeStatus.Running;
                            })),
                        new NpcActionLeaf("idzie na miejsce", c =>
                        {
                            ChampionPlan plan = c.Get<ChampionPlan>();
                            Position at = plan.Anchor!.Value;
                            bool walk = plan.Advancing && !plan.Recalling && Flat(c) < WalkWithin;
                            c.Mob.SetFocus(at, FocusKind.Wander);
                            if (Flat(c) > FarRoute) return c.Commands.PathTo(c.Npc, null, at, new Distance(Arrived - 30f), walk: false, TimeSpan.FromSeconds(6));
                            return c.Commands.MoveTo(c.Npc, at, new Distance(Arrived - 30f), walk, TimeSpan.FromSeconds(3));
                        }, TimeSpan.FromSeconds(7), started: c => { ChampionPlan p = c.Get<ChampionPlan>(); p.Moving = true; p.StartedAt = c.Position; },
                        finished: (c, ok) =>
                        {
                            ChampionPlan plan = c.Get<ChampionPlan>();
                            if (ok || Flat(c.Position, plan.StartedAt) >= 300f) plan.MoveFails = 0;
                            else { plan.MoveFails++; plan.FailAt = c.Now; }
                        }))),
                new Do<MobContext>("stoi", c =>
                {
                    ChampionPlan plan = c.Get<ChampionPlan>();
                    plan.Moving = false;
                    c.Mob.Note = $"taktyka: {plan.Mode}";
                    if (c.Target is null) FaceEnemyBase(c);
                    return NodeStatus.Running;
                }))));
    }

    private static void FaceEnemyBase(MobContext c)
    {
        if (c.Mob.Lane is not { } lane) return;
        Position[] route = lane.RouteOf(c.Kind.Faction);
        float along = ChampionTactics.Progress(route, c.Position);
        Position ahead = ChampionTactics.PointAt(route, along + 600f, 0f);
        c.Commands.Face(c.Npc, c.Position, ahead);
    }

    private static float Flat(MobContext c) => Flat(c.Position, c.Get<ChampionPlan>().Anchor!.Value);

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public override void Dormant(MobContext c)
    {
        (ChampionPlan plan, Combatant? target) = ChampionTactics.Decide(c);
        TimeSpan previous = plan.LastVirtual == default ? c.Now : plan.LastVirtual;
        plan.LastVirtual = c.Now;
        plan.Moving = false;
        float step = RunSpeed(c) * (float)Math.Min((c.Now - previous).TotalSeconds, 1.0);
        if (target is { } enemy)
        {
            c.Target = enemy;
            c.Mob.Target = enemy.Character;
            c.Mob.Quarry = enemy.Character;
            c.Behaviour.Get<ChampionSkills>()?.CastDormant(c);
            float gap = Flat(c.Position, enemy.Position) - (enemy.Mob?.Kind.Radius ?? 0f) - DormantSkirmish.Reach(c);
            if (gap > ChaseByRoute)
            {
                DormantWalk(c, enemy.Position, step);
                c.Mob.Note = $"bez hosta: biegnie do {enemy.Name}";
                return;
            }
            if (DormantSkirmish.Engage(c, enemy, step)) return;
        }
        c.Mob.Target = null;
        if (plan.Anchor is not { } anchor) return;
        float distance = Flat(c.Position, anchor);
        c.Mob.Note = $"bez hosta: {plan.Mode}";
        if (step <= 0 || distance <= Arrived) return;
        if (!plan.Recalling && !plan.Retreating)
            foreach (Combatant other in c.Mobs)
                if (other.Mob is { Lane: not null } mob && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)
                    && c.Position.DistanceTo(other.Position).Centimeters <= LaneSettings.ContactRange.Centimeters
                    && Flat(other.Position, anchor) < distance) return;
        DormantWalk(c, anchor, step);
    }

    private const float ChaseByRoute = 600f;

    internal static float RunSpeed(MobContext c) =>
        c.Director.Pace.Speed / MathF.Max(0.5f, LaneSettings.PaceFactor) * (c.Now < c.Mob.HasteUntil ? 1f + c.Mob.Haste : 1f);

    private sealed class DormantRoute
    {
        public List<Position> Points = [];
        public Position Goal;
        public TimeSpan At;
        public float Lift;
    }

    private static void DormantWalk(MobContext c, Position goal, float step)
    {
        if (step <= 0) return;
        DormantRoute route = c.Get<DormantRoute>();
        if (route.Points.Count == 0 || Flat(route.Goal, goal) > 300f || c.Now - route.At > TimeSpan.FromSeconds(3))
        {
            route.Goal = goal;
            route.At = c.Now;
            route.Points = MobCommands.Navigator?.Invoke(c.Position, goal) is { Count: > 0 } found ? [.. found] : [];
            route.Lift = route.Points.Count > 0 ? Math.Clamp(c.Position.Y - route.Points[0].Y, -50f, 200f) : 0f;
            if (route.Points.Count == 0) route.Points.Add(new Position(goal.X, goal.Y - route.Lift, goal.Z));
        }
        Position at = c.Position;
        while (step > 0 && route.Points.Count > 0)
        {
            Position point = route.Points[0];
            var next = new Position(point.X, point.Y + route.Lift, point.Z);
            float distance = Flat(at, next);
            if (distance <= step) { at = next; step -= distance; route.Points.RemoveAt(0); continue; }
            float k = step / distance;
            at = new Position(at.X + (next.X - at.X) * k, at.Y + (next.Y - at.Y) * k, at.Z + (next.Z - at.Z) * k);
            step = 0;
        }
        c.Npc.Position = at;
        c.Mob.LastPosition = at;
    }
}
