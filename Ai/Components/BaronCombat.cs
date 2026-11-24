namespace BotArena.Ai.Components;

public sealed class BaronState
{
    public TimeSpan BusyUntil;
    public TimeSpan NextAttack;
    public int Attacks;
    public int Ability;
    public int Form;
    public bool Roared;
    public bool Placed;
    public Character? GazeTarget;
    public Character? Aggro;
    public TimeSpan GazeUntil;
    public readonly List<(TimeSpan Due, Action<MobContext> Do)> Events = [];
}

public sealed class BaronCombat : MobComponent
{
    public override string Name => "baron: pociski, kałuże, klatka Pustki";

    private static void Play(MobContext c, string animation) => c.Director.Animate(c.Mob, animation);

    private static NodeStatus Stand(MobContext c) => NodeStatus.Running;

    private static NodeStatus Busy(MobContext c, TimeSpan length)
    {
        c.Commands.Cancel(c.Npc);
        c.Get<BaronState>().BusyUntil = c.Now + length;
        c.Mob.LastCombat = c.Now;
        return NodeStatus.Running;
    }

    private static float Flat(Position a, Position b) { float dx = a.X - b.X, dz = a.Z - b.Z; return MathF.Sqrt(dx * dx + dz * dz); }

    private static List<Combatant> Foes(MobContext c, float reach, bool championsOnly = false)
    {
        var list = new List<Combatant>();
        foreach (Combatant player in c.Players)
            if (!player.Character.IsDead && !Teams.Untargetable(player.Character) && Flat(c.Position, player.Position) <= reach) list.Add(player);
        foreach (Combatant other in c.Mobs)
            if (other.Mob is { } mob && !ReferenceEquals(mob, c.Mob) && !mob.Kind.Immobile && mob.Kind.Faction is Faction.Blue or Faction.Red
                && (!championsOnly || mob.Champion) && !other.Character.IsDead && Flat(c.Position, other.Position) <= reach) list.Add(other);
        return list;
    }

    private static Character? RefreshAggro(MobContext c)
    {
        BaronState state = c.Get<BaronState>();
        float reach = BaronSettings.AttackRange.Centimeters;
        if (state.Aggro is { IsValid: true, IsDead: false } held && !Teams.Untargetable(held) && Flat(c.Position, held.Position) <= reach + BaronSettings.AggroSlack.Centimeters)
            return held;
        List<Combatant> foes = Foes(c, reach);
        Combatant? pick = foes.Where(f => c.Target is { } t && ReferenceEquals(f.Character, t.Character)).Cast<Combatant?>().FirstOrDefault()
                          ?? foes.Where(f => f.Mob is null || f.Mob.Champion).OrderBy(f => Flat(c.Position, f.Position)).Cast<Combatant?>().FirstOrDefault()
                          ?? foes.OrderBy(f => Flat(c.Position, f.Position)).Cast<Combatant?>().FirstOrDefault();
        state.Aggro = pick?.Character ?? c.Target?.Character;
        return state.Aggro;
    }

    private static void After(MobContext c, TimeSpan delay, Action<MobContext> action) =>
        c.Get<BaronState>().Events.Add((c.Now + delay, action));

    private static void Hit(MobContext c, Character victim, float share, bool magic, int stacks, TimeSpan? at = null)
    {
        if (c.Get<BaronState>().Aggro is { } main && !ReferenceEquals(main, victim)) share *= BaronSettings.OthersShare;
        int amount = Math.Max(1, (int)Math.Round(BaronSettings.AttackDamage * share));
        c.Director.ScheduleDamage(victim, amount, at ?? c.Now, c.Npc, magic);
        if (stacks > 0) c.Director.AddCorruption(victim, stacks, c.Now);
    }

    private static void Corrode(MobContext c, Character basicTarget)
    {
        Combatant? pick = null;
        int least = int.MaxValue;
        float nearest = float.MaxValue;
        foreach (Combatant foe in Foes(c, BaronSettings.AttackRange.Centimeters + 300f))
        {
            int stacks = c.Director.CorruptionOf(foe.Character, c.Now);
            float distance = Flat(c.Position, foe.Position);
            if (stacks < least || (stacks == least && distance < nearest)) { pick = foe; least = stacks; nearest = distance; }
        }
        Character victim = pick?.Character ?? basicTarget;
        Hit(c, victim, BaronSettings.CorrosionShare, magic: true, stacks: 1);
        c.Director.ChampionEffect(c.Mob, victim, 2, BaronSettings.CorrosionFx);
    }

    public override void Sense(MobContext c)
    {
        BaronState state = c.Get<BaronState>();
        if (!state.Placed)
        {
            state.Placed = true;
            c.Npc.SetAngle(new Angle(BaronSettings.PitAngle));
        }
        for (int i = state.Events.Count - 1; i >= 0; i--)
        {
            var (due, action) = state.Events[i];
            if (due > c.Now) continue;
            state.Events.RemoveAt(i);
            try { action(c); } catch (Exception error) { c.Mob.Note = "baron: " + error.Message; }
        }
    }

    private static NodeStatus BasicAttack(MobContext c)
    {
        BaronState state = c.Get<BaronState>();
        state.NextAttack = c.Now + c.Jitter(BaronSettings.AttackCooldown);
        state.Attacks++;
        Character target = RefreshAggro(c) ?? c.Target!.Value.Character;
        state.GazeTarget = target;
        state.GazeUntil = c.Now + BaronSettings.GazeLength;
        After(c, BaronSettings.BoltRelease, cc =>
        {
            int effects = 0;
            foreach (Combatant foe in Foes(cc, BaronSettings.AttackRange.Centimeters).OrderBy(f => Flat(cc.Position, f.Position)))
            {
                Character victim = foe.Character;
                float distance = Flat(cc.Position, foe.Position);
                if (distance <= BaronSettings.MeleeRange.Centimeters + BaronSettings.Radius)
                {
                    Hit(cc, victim, 1f, magic: false, stacks: 1);
                    continue;
                }
                if (effects++ < MaxBoltEffects) cc.Director.ChampionEffect(cc.Mob, victim, 0, BaronSettings.BoltFx);
                TimeSpan flight = TimeSpan.FromSeconds(Math.Clamp(distance / BaronSettings.BoltSpeed, 0.1f, 1.2f));
                Hit(cc, victim, 1f, magic: false, stacks: 2, cc.Now + flight);
            }
            if (target.IsValid && !target.IsDead) Corrode(cc, target);
        });
        Play(c, BaronSettings.AttackAnimation);
        return Busy(c, BaronSettings.AttackAnimLength);
    }

    private const int MaxBoltEffects = 8;
    private const int MaxMarks = 6;

    private static NodeStatus Cast(MobContext c)
    {
        BaronState state = c.Get<BaronState>();
        state.Attacks = 0;
        state.NextAttack = c.Now + c.Jitter(BaronSettings.AttackCooldown) + TimeSpan.FromSeconds(1);
        RefreshAggro(c);
        int which = state.Ability++ % 4;
        Play(c, BaronSettings.CastAnimation);
        switch (which)
        {
            case 0: return AcidPool(c);
            case 1:
                int form = state.Form++ % 3;
                return form switch { 0 => Hunting(c), 1 => Territorial(c), _ => AllSeeing(c) };
            case 2: return AcidShot(c);
            default: return Quake(c);
        }
    }

    private static List<Position> Marks(MobContext c, float reach)
    {
        Character? main = c.Get<BaronState>().Aggro;
        return [.. Foes(c, reach).OrderByDescending(f => ReferenceEquals(f.Character, main)).ThenBy(f => Flat(c.Position, f.Position)).Take(MaxMarks).Select(f => f.Position)];
    }

    private static NodeStatus AcidPool(MobContext c)
    {
        float radius = BaronSettings.AcidPoolRadius.Centimeters;
        List<Position> spots = Marks(c, BaronSettings.AcidPoolRange.Centimeters);
        foreach (Position spot in spots) c.Director.GroundEffect(c.Mob, spot, BaronSettings.AcidPoolFx);
        After(c, BaronSettings.AcidPoolWindup, cc =>
        {
            var struck = new HashSet<Character>();
            foreach (Position spot in spots)
            {
                cc.Director.GroundEffect(cc.Mob, spot, BaronSettings.AcidPoolFx);
                foreach (Combatant foe in Foes(cc, BaronSettings.AttackRange.Centimeters + 600f))
                {
                    if (Flat(foe.Position, spot) > radius || !struck.Add(foe.Character)) continue;
                    Hit(cc, foe.Character, 1f, magic: true, stacks: 1);
                    cc.Director.Slow(foe.Character, BaronSettings.AcidPoolSlow, cc.Now + BaronSettings.AcidPoolField);
                }
            }
        });
        return Busy(c, BaronSettings.AcidPoolWindup + BaronSettings.CastBusy);
    }

    private static NodeStatus AcidShot(MobContext c)
    {
        float reach = BaronSettings.AcidShotLength.Centimeters;
        Position origin = c.Position;
        var struck = new HashSet<Character>();
        int steps = 4;
        for (int i = 0; i < steps; i++)
        {
            TimeSpan when = BaronSettings.AcidShotWindup + BaronSettings.AcidShotStream * ((i + 1d) / steps);
            After(c, when, cc =>
            {
                foreach (Position spot in Marks(cc, reach)) cc.Director.GroundEffect(cc.Mob, spot, BaronSettings.AcidPoolFx);
                foreach (Combatant foe in Foes(cc, reach))
                    if (struck.Add(foe.Character)) Hit(cc, foe.Character, 1f, magic: true, stacks: 1);
            });
        }
        return Busy(c, BaronSettings.AcidShotWindup + BaronSettings.AcidShotStream);
    }

    private static NodeStatus Quake(MobContext c)
    {
        List<Position> spots = Marks(c, BaronSettings.AttackRange.Centimeters + 600f);
        foreach (Position spot in spots) c.Director.GroundEffect(c.Mob, spot, BaronSettings.QuakeFx);
        After(c, BaronSettings.QuakeDelay, cc =>
        {
            var struck = new HashSet<Character>();
            foreach (Combatant foe in Foes(cc, BaronSettings.AttackRange.Centimeters + 600f))
            {
                if (!spots.Any(spot => Flat(foe.Position, spot) <= BaronSettings.QuakeRadius.Centimeters) || !struck.Add(foe.Character)) continue;
                Hit(cc, foe.Character, 1f, magic: true, stacks: 1);
                cc.Director.Immobilize(foe.Character, cc.Now + BaronSettings.QuakeDelay);
            }
        });
        return Busy(c, BaronSettings.QuakeDelay + BaronSettings.CastBusy);
    }

    private static NodeStatus Hunting(MobContext c)
    {
        List<Combatant> victims = Foes(c, BaronSettings.RiftRange.Centimeters, championsOnly: false)
            .Where(f => f.Mob is null || f.Mob.Champion).OrderBy(f => Flat(c.Position, f.Position)).ToList();
        float share = BaronSettings.HuntingShare;
        for (int i = 0; i < victims.Count; i++)
        {
            Character victim = victims[i].Character;
            float factor = share / MathF.Pow(2f, i);
            After(c, BaronSettings.HuntingFirst + BaronSettings.HuntingEvery * i, cc =>
            {
                if (!victim.IsValid || victim.IsDead) return;
                cc.Director.ChampionEffect(cc.Mob, victim, 2, BaronSettings.HuntingFx);
                int current = cc.Director.HealthOf(victim);
                cc.Director.ScheduleDamage(victim, Math.Max(1, (int)(current * factor)), cc.Now, cc.Npc, magic: true);
                cc.Director.AddCorruption(victim, 1, cc.Now);
            });
        }
        return Busy(c, BaronSettings.HuntingFirst + BaronSettings.CastBusy);
    }

    private static NodeStatus Territorial(MobContext c)
    {
        After(c, BaronSettings.TerritorialLean + BaronSettings.TerritorialAim, cc =>
        {
            float ring = BaronSettings.Radius + 260f;
            foreach (Combatant foe in Foes(cc, BaronSettings.TerritorialRange.Centimeters))
            {
                float px = foe.Position.X - cc.Position.X, pz = foe.Position.Z - cc.Position.Z, d = MathF.Max(1f, MathF.Sqrt(px * px + pz * pz));
                float stop = MathF.Min(d, ring);
                var to = new Position(cc.Position.X + px / d * stop, foe.Position.Y, cc.Position.Z + pz / d * stop);
                cc.Director.Pull(foe.Character, to, BaronSettings.TerritorialPull);
                Hit(cc, foe.Character, BaronSettings.TerritorialShare, magic: true, stacks: 1);
                cc.Director.ChampionEffect(cc.Mob, foe.Character, 2, BaronSettings.CageHitFx);
                cc.Director.GroundEffect(cc.Mob, to, BaronSettings.CageFx);
                cc.Director.Immobilize(foe.Character, cc.Now + BaronSettings.TerritorialPull + TimeSpan.FromSeconds(BaronSettings.CageSeconds));
            }
        });
        return Busy(c, BaronSettings.TerritorialLean + BaronSettings.TerritorialAim + BaronSettings.TerritorialPull);
    }

    private static NodeStatus AllSeeing(MobContext c)
    {
        List<Combatant> far = Foes(c, BaronSettings.RiftRange.Centimeters).Where(f => f.Mob is null || f.Mob.Champion)
            .OrderByDescending(f => Flat(c.Position, f.Position)).Take(2).ToList();
        foreach (Combatant foe in far)
        {
            Character victim = foe.Character;
            After(c, TimeSpan.FromMilliseconds(600), cc =>
            {
                if (!victim.IsValid || victim.IsDead) return;
                cc.Director.GroundEffect(cc.Mob, victim.Position, BaronSettings.RiftFx);
                cc.Director.ScheduleDamage(victim, BaronSettings.RiftFirst, cc.Now, cc.Npc, magic: true);
                cc.Director.AddCorruption(victim, 1, cc.Now);
                Position spot = victim.Position;
                for (int tick = 1; tick <= BaronSettings.RiftTicks; tick++)
                    After(cc, BaronSettings.RiftEvery * tick, c3 =>
                    {
                        foreach (Combatant inside in Foes(c3, BaronSettings.RiftRange.Centimeters + 200f))
                            if (Flat(inside.Position, spot) <= 200f) c3.Director.ScheduleDamage(inside.Character, BaronSettings.RiftTick, c3.Now, c3.Npc, magic: true);
                    });
            });
        }
        return Busy(c, BaronSettings.CastBusy * 3);
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, Combat.Fight(_ => BaronSettings.AttackRange,
            new Selector<MobContext>("walka barona",
                new Sequence<MobContext>("zajęty",
                    new Condition<MobContext>("rzuca?", c => c.Now < c.Get<BaronState>().BusyUntil),
                    new Do<MobContext>("czeka na koniec rzutu", Stand)),
                new Sequence<MobContext>("w zasięgu",
                    new Condition<MobContext>("cel w zasięgu?", c => c.TargetDistance <= BaronSettings.AttackRange),
                    new Selector<MobContext>("atakuje albo celuje",
                        new Sequence<MobContext>("zdolność",
                            new Condition<MobContext>("gotowy?", c => c.Now >= c.Get<BaronState>().NextAttack && c.Get<BaronState>().Attacks >= BaronSettings.AbilityEvery - 1),
                            new Do<MobContext>("rzuca zdolność", Cast)),
                        new Sequence<MobContext>("atak",
                            new Condition<MobContext>("gotowy?", c => c.Now >= c.Get<BaronState>().NextAttack),
                            new Do<MobContext>("atakuje", BasicAttack)),
                        new Do<MobContext>("stoi (nie obraca się)", Stand))),
                new Do<MobContext>("stoi w jamie (nie chodzi)", Stand))));
    }

    public override void Calm(MobContext c)
    {
        BaronState state = c.Get<BaronState>();
        state.Roared = false;
        state.Attacks = 0;
        state.Events.Clear();
        state.GazeTarget = null;
        state.Aggro = null;
    }

    public override void Died(Mob mob, Character? killer)
    {
        BaronState state = mob.Memory.Get<BaronState>();
        state.Events.Clear();
        state.BusyUntil = default;
        state.Attacks = 0;
        state.GazeTarget = null;
        state.Aggro = null;
    }

    public override float IncomingDamage(Mob mob, Character? attacker, TimeSpan now)
    {
        BaronState state = mob.Memory.Get<BaronState>();
        return attacker is not null && ReferenceEquals(state.GazeTarget, attacker) && now < state.GazeUntil ? 1f - BaronSettings.GazeReduction : 1f;
    }
}

public sealed class BaronReset : MobComponent
{
    private sealed class ResetState { public TimeSpan NextHeal; public bool Faced; }

    public override string Name => $"reset w jamie ({BaronSettings.ResetRegen * 100:0}%/s)";

    public override void Calm(MobContext c)
    {
        ResetState state = c.Get<ResetState>();
        if (c.Now - c.Mob.LastCombat < BaronSettings.ResetAfter) { state.Faced = false; return; }
        if (!state.Faced && c.Now >= c.Get<BaronState>().BusyUntil)
        {
            state.Faced = true;
            c.Npc.SetAngle(new Angle(BaronSettings.PitAngle));
        }
        if (c.State.Health >= c.State.MaxHealth || c.Now < state.NextHeal) return;
        state.NextHeal = c.Now + TimeSpan.FromSeconds(1);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * BaronSettings.ResetRegen)));
    }
}
