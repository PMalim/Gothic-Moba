namespace BotArena.Ai.Components;

public sealed class CombatState
{
    public TimeSpan NextAttack;
    public int? AttackAction;
    public TimeSpan NextDodge;
    public TimeSpan ReachedUntil;
    public TimeSpan ChaseBlockedUntil;
    public TimeSpan RouteRetryAt;
    public int FailedChases;
    public int CircleSide = 1;
}

public static class Combat
{
    private static readonly TimeSpan ReachedTrust = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan BlockedFor = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan ChaseTimeout = TimeSpan.FromSeconds(10);
    public const int GiveUpAfter = 4;
    public static readonly TimeSpan GiveUpTime = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan IgnoreAfterGiveUp = TimeSpan.FromSeconds(20);

    public static bool AttackReady(MobContext c)
    {
        CombatState state = c.Get<CombatState>();
        return c.Now >= state.NextAttack || (state.AttackAction is { } action && c.Mob.CurrentAction == action);
    }

    public static void AttackStarted(MobContext c, TimeSpan cooldown, TimeSpan? hitDelay = null)
    {
        CombatState state = c.Get<CombatState>();
        state.NextAttack = c.Now + c.Jitter(cooldown);
        state.AttackAction = c.Mob.CurrentAction;
        if (c.Target is { Mob.Kind.Immobile: true } target && (c.Kind.Damage is not null || c.Kind.Role is not null))
            c.Director.ScheduleDamage(target.Character, c.Director.AttackDamage(c.Mob, target.Character), c.Now + (hitDelay ?? MeleeHitDelay));
    }

    public static readonly TimeSpan MeleeHitDelay = TimeSpan.FromMilliseconds(450);

    public static Node<MobContext> Fight(Func<MobContext, Distance> reach, Node<MobContext> style) => new Sequence<MobContext>("walka",
        new Condition<MobContext>("ma cel?", c => c.Target is not null),
        new Do<MobContext>("namierza", c =>
        {
            c.Mob.LastCombat = c.Now;
            if (c.TargetDistance <= reach(c) && c.TargetKnown is { } known)
            {
                known.UnreachableSince = null;
                c.Get<CombatState>().FailedChases = 0;
            }
            return NodeStatus.Success;
        }),
        new Selector<MobContext>("broń",
            new Condition<MobContext>("broń gotowa?", c => c.Mob.WeaponOut || !c.Kind.Humanoid || c.Behaviour.Has<CastCombat>()),
            new NpcActionLeaf("dobywa broni", c => c.Commands.DrawWeapon(c.Npc, MobBrain.CombatMode(c.Kind)), TimeSpan.FromSeconds(2),
                finished: (c, _) => c.Mob.WeaponOut = true)),
        style);

    public static Node<MobContext> Approach(string name, Func<MobContext, Distance> stop) => new Sequence<MobContext>(name + " (dojście)",
        new Do<MobContext>("ocenia drogę", c =>
        {
            CombatState state = c.Get<CombatState>();
            if (state.FailedChases < GiveUpAfter || c.TargetKnown?.UnreachableSince is not { } since || c.Now - since < GiveUpTime)
                return NodeStatus.Success;
            GiveUp(c);
            return NodeStatus.Failure;
        }),
        new Selector<MobContext>("droga",
            new Sequence<MobContext>("pościg",
                new Condition<MobContext>("droga wolna?", c => c.Now >= c.Get<CombatState>().ChaseBlockedUntil),
                new NpcActionLeaf(name, c => c.Commands.Chase(c.Npc, c.Target!.Value.Character, new Distance(stop(c).Centimeters + c.TargetRadius), walk: false, ChaseTimeout, c.Mob.FlankAngle),
                    ChaseTimeout + TimeSpan.FromSeconds(1), finished: (c, reached) =>
                    {
                        CombatState state = c.Get<CombatState>();
                        if (reached)
                        {
                            state.ReachedUntil = c.Now + ReachedTrust;
                            state.FailedChases = 0;
                            if (c.TargetKnown is { } known) known.UnreachableSince = null;
                        }
                        else if (c.ActionElapsed < ChaseTimeout * 0.9 && c.TargetDistance > stop(c))
                        {
                            state.ChaseBlockedUntil = c.Now + BlockedFor;
                            state.FailedChases++;
                            if (c.TargetKnown is { } known) known.UnreachableSince ??= c.Now;
                        }
                    })),
            new Sequence<MobContext>("obejście",
                new Condition<MobContext>("pora na trasę?", c => c.Now >= c.Get<CombatState>().RouteRetryAt),
                new NpcActionLeaf("obchodzi przeszkodę", c =>
                    {
                        Combatant target = c.Target!.Value;
                        c.Mob.SetFocus(target.Position, FocusKind.LastSeen);
                        return c.Commands.PathTo(c.Npc, target.Character, target.Position, new Distance(stop(c).Centimeters + c.TargetRadius), walk: false, TimeSpan.FromSeconds(20));
                    },
                    TimeSpan.FromSeconds(22), finished: (c, ok) =>
                    {
                        CombatState state = c.Get<CombatState>();
                        if (ok) state.ChaseBlockedUntil = default;
                        else state.RouteRetryAt = c.Now + BlockedFor;
                    })),
            new Do<MobContext>("czeka pod celem", c => { FaceTarget(c); return NodeStatus.Running; })));

    public static void GiveUp(MobContext c)
    {
        if (c.Target is { } target) c.Memory.Ignore(target.Character, c.Now + IgnoreAfterGiveUp);
        c.Target = null;
        c.Mob.Target = null;
        c.Mob.Quarry = null;
        CombatState state = c.Get<CombatState>();
        state.FailedChases = 0;
        state.ChaseBlockedUntil = default;
        state.RouteRetryAt = default;
        Alert.Begin(c, c.Position);
    }

    public static NodeStatus FaceTarget(MobContext c)
    {
        if (c.Target is { } target) c.Commands.Face(c.Npc, c.Position, target.Position);
        return NodeStatus.Running;
    }
}

public sealed class MeleeCombat(bool circles = false) : MobComponent
{
    private static readonly TimeSpan CircleEvery = TimeSpan.FromSeconds(2.5);
    public override string Name => circles ? "walka wręcz (krąży)" : "walka wręcz";

    public override IEnumerable<Branch> Branches()
    {
        var between = new List<Node<MobContext>>
        {
            new Sequence<MobContext>("atak",
                new Condition<MobContext>("gotowy do ciosu?", Combat.AttackReady),
                new NpcActionLeaf("uderza", c => c.Commands.AttackMelee(c.Npc, c.Target!.Value.Character,
                        c.Kind.Attacks[c.Random.Next(c.Kind.Attacks.Length)], c.Kind.Combo), TimeSpan.FromSeconds(3),
                    started: c => Combat.AttackStarted(c, c.Kind.AttackCooldown))),
        };
        if (circles)
            between.Add(new Sequence<MobContext>("krążenie",
                new Condition<MobContext>("ma chwilę?", c => c.Get<CombatState>().NextAttack - c.Now > TimeSpan.FromMilliseconds(600)),
                new Cooldown<MobContext>(CircleEvery, new NpcActionLeaf("krąży wokół celu", Circle, TimeSpan.FromSeconds(2)))));
        between.Add(new Do<MobContext>("czeka na cios", Combat.FaceTarget));

        yield return new Branch(Priority.Combat, Combat.Fight(c => new Distance(c.Kind.AttackRange.Centimeters * 1.2f),
            new Selector<MobContext>("walka wręcz",
                new Sequence<MobContext>("cios",
                    new Condition<MobContext>("w zasięgu ciosu?", c => c.Now < c.Get<CombatState>().ReachedUntil || c.TargetDistance <= c.Kind.AttackRange),
                    new Selector<MobContext>("atak albo czekanie", [.. between])),
                Combat.Approach("goni", c => new Distance(c.Kind.AttackRange.Centimeters * 0.85f)))));
    }

    private static int Circle(MobContext c)
    {
        CombatState state = c.Get<CombatState>();
        Position target = c.Target!.Value.Position;
        float dx = c.Position.X - target.X, dz = c.Position.Z - target.Z;
        float length = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        float angle = state.CircleSide * 50f * MathF.PI / 180f;
        state.CircleSide = -state.CircleSide;
        float ux = dx / length, uz = dz / length, ring = c.Kind.AttackRange.Centimeters * 0.9f;
        var point = new Position(target.X + (ux * MathF.Cos(angle) - uz * MathF.Sin(angle)) * ring, c.Position.Y,
            target.Z + (ux * MathF.Sin(angle) + uz * MathF.Cos(angle)) * ring);
        c.Mob.SetFocus(point, FocusKind.Circle);
        return c.Commands.MoveTo(c.Npc, point, new Distance(60), walk: true, TimeSpan.FromMilliseconds(1500));
    }
}

public sealed class RangedCombat(Distance preferred) : MobComponent
{
    public override string Name => $"strzelec {preferred.Centimeters / 100:0} m";
    public Distance Preferred => preferred;

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, Combat.Fight(_ => new Distance(preferred.Centimeters + 300),
            new Selector<MobContext>("walka na dystans",
                new Sequence<MobContext>("odskok",
                    new Condition<MobContext>("za blisko?", c => c.TargetDistance < new Distance(450) && c.Now >= c.Get<CombatState>().NextDodge),
                    new NpcActionLeaf("odskakuje", c =>
                        {
                            Position threat = c.Target!.Value.Position;
                            c.Mob.SetFocus(threat, FocusKind.Threat);
                            return c.Commands.FleeFrom(c.Npc, threat, new Distance(preferred.Centimeters * 0.7f), TimeSpan.FromMilliseconds(1500));
                        },
                        TimeSpan.FromSeconds(2), started: c => c.Get<CombatState>().NextDodge = c.Now + TimeSpan.FromSeconds(4))),
                new Sequence<MobContext>("strzał",
                    new Condition<MobContext>("na dystansie strzału?", c => c.TargetDistance.Centimeters <= preferred.Centimeters + 300),
                    new Selector<MobContext>("strzał albo celowanie",
                        new Sequence<MobContext>("strzela",
                            new Condition<MobContext>("gotowy do strzału?", Combat.AttackReady),
                            new NpcActionLeaf("strzela", c => c.Commands.AttackRanged(c.Npc, c.Target!.Value.Character), TimeSpan.FromSeconds(4),
                                started: c => Combat.AttackStarted(c, c.Kind.AttackCooldown))),
                        new Do<MobContext>("celuje", Combat.FaceTarget))),
                Combat.Approach("podchodzi", _ => preferred))));
    }
}
