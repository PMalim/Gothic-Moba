namespace BotArena.Ai.Components;

public sealed class DrakeState
{
    public TimeSpan BusyUntil;
    public TimeSpan NextAttack;
    public TimeSpan NextBreath;
    public TimeSpan NextHop;
    public bool Roared;
    public bool Landed;
    public readonly List<(TimeSpan Due, float Dx, float Dz)> BreathTicks = [];
    public readonly List<(TimeSpan Due, Character Target)> Spits = [];
}

public sealed class DrakeCombat : MobComponent
{
    public override string Name => "smok: ugryzienie, kula ognia, zionięcie";

    private static void Play(MobContext c, string animation) => c.Director.Animate(c.Mob, animation);

    private static NodeStatus Busy(MobContext c, string animation, TimeSpan length)
    {
        c.Commands.Cancel(c.Npc);
        Combat.FaceTarget(c);
        Play(c, animation);
        c.Get<DrakeState>().BusyUntil = c.Now + length;
        c.Mob.LastCombat = c.Now;
        return NodeStatus.Running;
    }

    public override void Sense(MobContext c)
    {
        DrakeState state = c.Get<DrakeState>();
        if (!state.Landed)
        {
            state.Landed = true;
            Play(c, "T_DRAKE_LAND");
            state.BusyUntil = c.Now + DrakeSettings.LandLength;
        }
        for (int i = state.Spits.Count - 1; i >= 0; i--)
        {
            var (due, target) = state.Spits[i];
            if (due > c.Now) continue;
            state.Spits.RemoveAt(i);
            if (!target.IsValid || target.IsDead) continue;
            c.Director.ChampionEffect(c.Mob, target, 0, DrakeSettings.SpitFx);
            float distance = c.Position.DistanceTo(target.Position).Centimeters;
            TimeSpan flight = TimeSpan.FromSeconds(Math.Clamp(distance / DrakeSettings.SpitSpeed, 0.1f, 1.5f));
            c.Director.ScheduleDamage(target, DrakeSettings.SpitDamage, c.Now + flight, c.Npc, magic: true);
        }
        for (int i = state.BreathTicks.Count - 1; i >= 0; i--)
        {
            var (due, dx, dz) = state.BreathTicks[i];
            if (due > c.Now) continue;
            state.BreathTicks.RemoveAt(i);
            foreach (Combatant victim in InCone(c, dx, dz))
                c.Director.ScheduleDamage(victim.Character, DrakeSettings.BreathDamagePerTick, c.Now, c.Npc, magic: true);
        }
    }

    private static IEnumerable<Combatant> InCone(MobContext c, float dx, float dz)
    {
        float length = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        float ux = dx / length, uz = dz / length, cos = MathF.Cos(DrakeSettings.BreathHalfAngle * MathF.PI / 180f);
        float reach = DrakeSettings.BreathRange.Centimeters;
        bool Inside(Position at)
        {
            float px = at.X - c.Position.X, pz = at.Z - c.Position.Z;
            float d = MathF.Sqrt(px * px + pz * pz);
            if (d > reach) return false;
            if (d < 120f) return true;
            return (px * ux + pz * uz) / d >= cos;
        }
        foreach (Combatant player in c.Players)
            if (!player.Character.IsDead && !Teams.Untargetable(player.Character) && Inside(player.Position)) yield return player;
        foreach (Combatant other in c.Mobs)
            if (other.Mob is { } mob && !ReferenceEquals(mob, c.Mob) && !mob.Kind.Immobile && mob.Kind.Faction is Faction.Blue or Faction.Red
                && !other.Character.IsDead && Inside(other.Position)) yield return other;
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, Combat.Fight(_ => DrakeSettings.SpitRange,
            new Selector<MobContext>("walka smoka",
                new Sequence<MobContext>("zajęty",
                    new Condition<MobContext>("gra animację?", c => c.Now < c.Get<DrakeState>().BusyUntil),
                    new Do<MobContext>("czeka na koniec animacji", _ => NodeStatus.Running)),
                new Sequence<MobContext>("ryk",
                    new Condition<MobContext>("pierwszy raz w tej walce?", c => !c.Get<DrakeState>().Roared),
                    new Do<MobContext>("ryczy", c =>
                    {
                        DrakeState state = c.Get<DrakeState>();
                        state.Roared = true;
                        state.NextBreath = c.Now + DrakeSettings.BreathFirstDelay;
                        return Busy(c, "T_DRAKE_ROAR", DrakeSettings.RoarLength);
                    })),
                new Sequence<MobContext>("odskok",
                    new Condition<MobContext>("wróg pod pyskiem?", c => c.TargetDistance <= DrakeSettings.HopTrigger && c.Now >= c.Get<DrakeState>().NextHop),
                    new Do<MobContext>("odskakuje w tył", c =>
                    {
                        c.Get<DrakeState>().NextHop = c.Now + DrakeSettings.HopCooldown;
                        return Busy(c, "T_FISTJUMPB", DrakeSettings.HopLength);
                    })),
                new Sequence<MobContext>("zionięcie",
                    new Condition<MobContext>("pora zionąć?", c => c.Now >= c.Get<DrakeState>().NextBreath
                        && c.TargetDistance.Centimeters <= DrakeSettings.BreathRange.Centimeters * 0.85f),
                    new Do<MobContext>("zieje ogniem", c =>
                    {
                        DrakeState state = c.Get<DrakeState>();
                        state.NextBreath = c.Now + c.Jitter(DrakeSettings.BreathCooldown);
                        state.NextAttack = c.Now + DrakeSettings.BreathLength;
                        Position target = c.Target!.Value.Position;
                        float dx = target.X - c.Position.X, dz = target.Z - c.Position.Z;
                        for (int tick = 0; tick < DrakeSettings.BreathTicks; tick++)
                            state.BreathTicks.Add((c.Now + DrakeSettings.BreathFirstTick + DrakeSettings.BreathTickEvery * tick, dx, dz));
                        return Busy(c, "T_DRAKE_BREATH", DrakeSettings.BreathLength);
                    })),
                new Sequence<MobContext>("ugryzienie",
                    new Condition<MobContext>("w zasięgu pyska?", c => c.TargetDistance <= DrakeSettings.BiteRange),
                    new Selector<MobContext>("gryzie albo czeka",
                        new Sequence<MobContext>("gryzie",
                            new Condition<MobContext>("gotowy?", c => c.Now >= c.Get<DrakeState>().NextAttack),
                            new Do<MobContext>("gryzie", c =>
                            {
                                c.Get<DrakeState>().NextAttack = c.Now + c.Jitter(DrakeSettings.BiteCooldown);
                                c.Director.ScheduleDamage(c.Target!.Value.Character, DrakeSettings.BiteDamage, c.Now + DrakeSettings.BiteHit, c.Npc, magic: false);
                                return Busy(c, "S_FISTATTACK", DrakeSettings.BiteLength);
                            })),
                        new Do<MobContext>("patrzy na cel", Combat.FaceTarget))),
                new Sequence<MobContext>("kula ognia",
                    new Condition<MobContext>("w zasięgu kuli?", c => c.TargetDistance <= DrakeSettings.SpitRange),
                    new Selector<MobContext>("pluje albo celuje",
                        new Sequence<MobContext>("pluje",
                            new Condition<MobContext>("gotowy?", c => c.Now >= c.Get<DrakeState>().NextAttack),
                            new Do<MobContext>("pluje ogniem", c =>
                            {
                                DrakeState state = c.Get<DrakeState>();
                                state.NextAttack = c.Now + c.Jitter(DrakeSettings.SpitCooldown);
                                state.Spits.Add((c.Now + DrakeSettings.SpitRelease, c.Target!.Value.Character));
                                return Busy(c, "T_DRAKE_SPIT", DrakeSettings.SpitLength);
                            })),
                        new Do<MobContext>("celuje", Combat.FaceTarget))),
                Combat.Approach("podchodzi", _ => new Distance(DrakeSettings.BiteRange.Centimeters * 0.8f)))));
    }

    public override void Calm(MobContext c)
    {
        DrakeState state = c.Get<DrakeState>();
        state.Roared = false;
        state.BreathTicks.Clear();
        state.Spits.Clear();
    }

    public override void Died(Mob mob, Character? killer)
    {
        DrakeState state = mob.Memory.Get<DrakeState>();
        state.Landed = false;
        state.Roared = false;
        state.BusyUntil = default;
        state.BreathTicks.Clear();
        state.Spits.Clear();
    }
}

public sealed class DrakeReset : MobComponent
{
    private sealed class ResetState
    {
        public TimeSpan NextHeal;
        public bool Faced;
    }

    public override string Name => $"reset w jamie ({DrakeSettings.ResetRegen * 100:0}%/s)";

    public override void Sense(MobContext c)
    {
        if (!c.Get<TerritoryState>().Returning) return;
        ResetState state = c.Get<ResetState>();
        if (c.State.Health >= c.State.MaxHealth || c.Now < state.NextHeal) return;
        state.NextHeal = c.Now + TimeSpan.FromMilliseconds(500);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * DrakeSettings.ReturnRegen * 0.5f)));
    }

    public override void Calm(MobContext c)
    {
        ResetState state = c.Get<ResetState>();
        if (c.FromHome.Centimeters > 400f) { state.Faced = false; return; }
        if (!state.Faced && c.Now >= c.Get<DrakeState>().BusyUntil)
        {
            state.Faced = true;
            c.Npc.SetAngle(new Angle(DrakeSettings.PitAngle));
        }
        if (c.State.Health >= c.State.MaxHealth || c.Now < state.NextHeal) return;
        state.NextHeal = c.Now + TimeSpan.FromSeconds(1);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * DrakeSettings.ResetRegen)));
    }
}
