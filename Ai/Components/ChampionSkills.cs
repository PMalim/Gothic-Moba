using BotArena.Heroes;

namespace BotArena.Ai.Components;

public sealed class ChampionState
{
    public TimeSpan[] Ready = new TimeSpan[4];
    public TimeSpan NextRegen;
    public readonly List<(TimeSpan Due, Action<MobContext> Act)> Later = [];
}

public sealed class ChampionSkills(HeroDef hero) : MobComponent
{
    private const float CooldownFactor = 1.25f;
    private const float ProjectileSpeed = 1800f;
    private static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(5);
    private const float FountainRange = 1800f;
    public override string Name => $"zdolności ({hero.Name})";
    public HeroDef Hero => hero;

    private const float ChaserRange = 900f;
    private const float EscapeWithin = 600f, EscapeBelow = 0.4f;

    public override void Sense(MobContext c)
    {
        RunLater(c);
        Regenerate(c);
        Survive(c);
    }

    private static void RunLater(MobContext c)
    {
        List<(TimeSpan Due, Action<MobContext> Act)> later = c.Get<ChampionState>().Later;
        for (int i = 0; i < later.Count; i++)
        {
            if (later[i].Due > c.Now) continue;
            Action<MobContext> act = later[i].Act;
            later.RemoveAt(i--);
            act(c);
        }
    }

    private static void After(MobContext c, TimeSpan delay, Action<MobContext> act) => c.Get<ChampionState>().Later.Add((c.Now + delay, act));

    public override void Dormant(MobContext c)
    {
        RunLater(c);
        Regenerate(c);
        Survive(c);
    }

    public void CastDormant(MobContext c)
    {
        if (c.Target is not null) Cast(c);
    }

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat - 10, new Sequence<MobContext>("zdolności",
            new Condition<MobContext>("ma cel?", c => c.Target is not null),
            new Do<MobContext>("używa zdolności", c => { Cast(c); return NodeStatus.Failure; })));
    }

    private static void Regenerate(MobContext c)
    {
        ChampionState state = c.Get<ChampionState>();
        if (c.Now < state.NextRegen || c.State.Health >= c.State.MaxHealth) return;
        bool atBase = AtBase(c);
        if (!atBase && c.Now - c.Mob.LastCombat < QuietTime) return;
        state.NextRegen = c.Now + TimeSpan.FromSeconds(1);
        c.Director.Heal(c.Mob, Math.Max(1, (int)(c.Mob.MaxHealth * (atBase ? 0.10f : 0.008f))));
    }

    public static bool AtBase(MobContext c)
    {
        if (c.Mob.Lane is { } lane && c.Position.DistanceTo(lane.SpawnOf(c.Kind.Faction)).Centimeters < FountainRange) return true;
        return LaneSettings.Lanes.Length > 0 && c.Position.DistanceTo(LaneSettings.Lanes[0].SpawnOf(c.Kind.Faction)).Centimeters < FountainRange;
    }

    private void Cast(MobContext c)
    {
        ChampionState state = c.Get<ChampionState>();
        Combatant target = c.Target!.Value;
        float distance = c.Position.DistanceTo(target.Position).Centimeters;
        List<Combatant> enemies = Enemies(c);
        for (int slot = 0; slot < hero.Abilities.Length && slot < state.Ready.Length; slot++)
        {
            AbilityDef ability = Ranked(c, slot);
            if (ability.IsPassive || c.Now < state.Ready[slot] || !TryCast(c, ability, slot, target, distance, enemies)) continue;
            state.Ready[slot] = c.Now + Cooldown(c, ability);
            c.Mob.LastCombat = c.Now;
            return;
        }
    }

    private void Survive(MobContext c)
    {
        ChampionPlan plan = c.Get<ChampionPlan>();
        if (!(plan.Retreating || plan.Recalling || c.Health < 0.35f)) return;
        float chaser = float.MaxValue;
        foreach (Combatant enemy in Enemies(c))
            if (enemy.IsPlayer || enemy.Mob?.Champion == true) chaser = MathF.Min(chaser, c.Position.DistanceTo(enemy.Position).Centimeters);
        if (chaser > ChaserRange) return;
        ChampionState state = c.Get<ChampionState>();
        for (int slot = 0; slot < hero.Abilities.Length && slot < state.Ready.Length; slot++)
        {
            AbilityDef ability = Ranked(c, slot);
            if (c.Now < state.Ready[slot]) continue;
            bool used = ability.Kind switch
            {
                AbilityKind.Shield => c.Health <= 0.7f && c.Now >= c.Mob.ShieldUntil && CastShield(c, ability),
                AbilityKind.Heal => c.Health < 0.6f && CastHeal(c, ability),
                AbilityKind.Dash or AbilityKind.Roll => c.Health < EscapeBelow && chaser <= EscapeWithin && Escape(c, ability),
                AbilityKind.Haste => StartHaste(c, ability),
                AbilityKind.Decoy => chaser <= EscapeWithin && CastDecoy(c, ability),
                AbilityKind.Ironhead => c.Health < 0.5f && Ironhead(c, ability),
                AbilityKind.Rampage => c.Health < 0.3f && c.Now >= c.Mob.BoostUntil && Rampage(c, ability),
                _ => false,
            };
            if (!used) continue;
            state.Ready[slot] = c.Now + Cooldown(c, ability);
            return;
        }
    }

    private AbilityDef Ranked(MobContext c, int slot)
    {
        int level = c.Mob.ChampionLevel;
        return HeroResources.AtRank(hero.Abilities[slot], level >= 9 ? 3 : level >= 5 ? 2 : 1);
    }

    private static TimeSpan Cooldown(MobContext c, AbilityDef ability) =>
        TimeSpan.FromSeconds(HeroResources.Cooldown(ability.Cooldown * CooldownFactor, c.Mob.ChampionStats?.Cdr ?? 0));

    private static bool Escape(MobContext c, AbilityDef ability)
    {
        if (c.Mob.Lane is not { } lane || ability.Range == 0) return false;
        Position[] route = lane.RouteOf(c.Kind.Faction);
        float along = ChampionTactics.Progress(route, c.Position);
        float travel = MathF.Abs(ability.Range);
        if (along < travel + 300f) return false;
        Position onRoute = ChampionTactics.PointAt(route, along, 0f);
        float dx = onRoute.X - c.Position.X, dz = onRoute.Z - c.Position.Z;
        if (dx * dx + dz * dz > 500f * 500f) return false;
        Position end = ChampionTactics.PointAt(route, along - travel, 0f);
        Fx(c, null, 1, ability);
        c.Npc.Position = end;
        c.Mob.LastPosition = end;
        return true;
    }

    private static bool CastShield(MobContext c, AbilityDef ability)
    {
        Fx(c, null, 1, ability);
        c.Director.Shield(c.Mob, c.Now + TimeSpan.FromSeconds(ability.Seconds), ability.Damage / 100f);
        return true;
    }

    private bool CastHeal(MobContext c, AbilityDef ability)
    {
        Fx(c, null, 1, ability);
        c.Director.Heal(c.Mob, HeroResources.AbilityHeal(ability, StatsOf(c)));
        return true;
    }

    private static List<Combatant> Enemies(MobContext c)
    {
        var list = new List<Combatant>();
        foreach (Combatant other in c.Mobs)
            if (other.Mob is { } mob && !ReferenceEquals(mob, c.Mob) && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction)
                && other.Character is { IsValid: true, IsDead: false }) list.Add(other);
        foreach (Combatant player in c.Players)
            if (Teams.IsEnemy(c.Kind.Faction, player.Character) && !Teams.Untargetable(player.Character)) list.Add(player);
        return list;
    }

    private bool TryCast(MobContext c, AbilityDef ability, int slot, Combatant target, float distance, List<Combatant> enemies)
    {
        bool duel = target.IsPlayer || target.Mob?.Champion == true;
        bool secures = target.Mob is { Kind.Immobile: false } victim && victim.LastHealth > 0 && victim.LastHealth <= HeroResources.AbilityDamage(ability, StatsOf(c));
        switch (ability.Kind)
        {
            case AbilityKind.Strike:
                if (distance > MathF.Min(ability.Range, LaneSettings.MaxAttackRange.Centimeters) || !(duel || secures)) return false;
                Fx(c, target.Character, 0, ability);
                Hurt(c, target, ability, TimeSpan.FromSeconds(distance / ProjectileSpeed + 0.1));
                return true;

            case AbilityKind.Rain:
                if (distance > MathF.Min(ability.Range, LaneSettings.MaxAttackRange.Centimeters) || !duel) return false;
                Fx(c, target.Character, 2, ability);
                foreach (Combatant enemy in enemies)
                    if (enemy.Position.DistanceTo(target.Position).Centimeters <= ability.Radius)
                        Hurt(c, enemy, ability, TimeSpan.FromMilliseconds(700));
                return true;

            case AbilityKind.Nova:
            {
                int near = enemies.Count(enemy => c.Position.DistanceTo(enemy.Position).Centimeters <= ability.Radius);
                bool championNear = enemies.Any(enemy => (enemy.IsPlayer || enemy.Mob?.Champion == true) && c.Position.DistanceTo(enemy.Position).Centimeters <= ability.Radius);
                if (!championNear && near < 3) return false;
                Fx(c, null, 1, ability);
                foreach (Combatant enemy in enemies)
                {
                    if (c.Position.DistanceTo(enemy.Position).Centimeters > ability.Radius) continue;
                    Hurt(c, enemy, ability, TimeSpan.FromMilliseconds(250));
                    if (ability.Seconds > 0 && enemy.Mob is { } mob && !mob.Kind.Immobile)
                        c.Director.Stun(mob, c.Now + TimeSpan.FromSeconds(ability.Seconds));
                }
                return true;
            }

            case AbilityKind.Dash:
            {
                if (ability.Range <= 0 || !duel || distance < 450 || distance > ability.Range + 300) return false;
                float dx = target.Position.X - c.Position.X, dz = target.Position.Z - c.Position.Z;
                float travel = MathF.Min(ability.Range, distance - 200);
                var end = new Position(c.Position.X + dx / distance * travel, target.Position.Y, c.Position.Z + dz / distance * travel);
                Fx(c, null, 1, ability);
                c.Npc.Position = end;
                c.Mob.LastPosition = end;
                foreach (Combatant enemy in enemies)
                    if (enemy.Position.DistanceTo(end).Centimeters <= ability.Radius) Hurt(c, enemy, ability, TimeSpan.FromMilliseconds(300));
                return true;
            }

            case AbilityKind.Heal:
            {
                if (c.Health >= 0.6f) return false;
                Fx(c, null, 1, ability);
                c.Director.Heal(c.Mob, HeroResources.AbilityHeal(ability, StatsOf(c)));
                foreach (Combatant ally in c.Mobs)
                    if (ally.Mob is { Champion: true } mate && !ReferenceEquals(mate, c.Mob) && mate.Kind.Faction == c.Kind.Faction
                        && c.Position.DistanceTo(ally.Position).Centimeters <= ability.Radius) c.Director.Heal(mate, HeroResources.AbilityHeal(ability, StatsOf(c)));
                foreach (Combatant player in c.Players)
                    if (player.Character is Player friend && !Teams.IsEnemy(c.Kind.Faction, friend) && !friend.IsDead
                        && c.Position.DistanceTo(player.Position).Centimeters <= ability.Radius)
                        friend.Health = Math.Min(friend.MaxHealth, friend.Health + HeroResources.AbilityHeal(ability, StatsOf(c)));
                return true;
            }

            case AbilityKind.Shield:
                if (c.Health > 0.7f || c.Now < c.Mob.ShieldUntil) return false;
                Fx(c, null, 1, ability);
                c.Director.Shield(c.Mob, c.Now + TimeSpan.FromSeconds(ability.Seconds), ability.Damage / 100f);
                return true;

            case AbilityKind.Empower:
            case AbilityKind.Bloodlust:
            {
                if (distance > MeleeReach(c) || !(duel || secures)) return false;
                Aura(c, ability.Fx, 1f);
                TimeSpan swing = TimeSpan.FromMilliseconds(450);
                int dealt = Hurt(c, target, ability, swing);
                if (ability.Kind == AbilityKind.Bloodlust && dealt > 0) After(c, swing, cc => cc.Director.Heal(cc.Mob, dealt));
                if (ability.Kind == AbilityKind.Empower) After(c, swing, cc => Fx(cc, target.Character, 2, HeroBuffs.EmpowerHitFx));
                return true;
            }

            case AbilityKind.Haste:
                if (!duel || distance < 500f || distance > 1600f) return false;
                return StartHaste(c, ability);

            case AbilityKind.Curse:
            {
                if (!duel || distance > ability.Range + ability.Radius * 0.5f) return false;
                Position center = distance <= ability.Range ? target.Position : Toward(c.Position, target.Position, ability.Range);
                c.Director.GroundEffect(c.Mob, center, ability.Fx);
                int tick = Boosted(c, HeroResources.AbilityDamage(ability, StatsOf(c)));
                bool magic = HeroResources.IsMagicAbility(hero, ability);
                for (TimeSpan at = TimeSpan.FromMilliseconds(300); at.TotalSeconds < ability.Seconds; at += HeroBuffs.CurseTick)
                    After(c, at, cc =>
                    {
                        foreach (Combatant enemy in Enemies(cc))
                            if (!IsStructure(enemy) && Flat(enemy.Position, center) <= ability.Radius)
                                cc.Director.ScheduleDamage(enemy.Character, tick, cc.Now, cc.Npc, magic);
                    });
                return true;
            }

            case AbilityKind.Rampage:
                if (!duel || distance > 700f || c.Now < c.Mob.BoostUntil) return false;
                return Rampage(c, ability);

            case AbilityKind.PierceShot:
            {
                if (!duel || distance > LaneSettings.MaxAttackRange.Centimeters) return false;
                Fx(c, target.Character, 0, ability);
                After(c, TimeSpan.FromSeconds(distance / ProjectileSpeed + 0.1), cc =>
                {
                    if (!Alive(target)) return;
                    cc.Director.Immobilize(target.Character, cc.Now + TimeSpan.FromSeconds(ability.Seconds));
                    Fx(cc, target.Character, 2, HeroBuffs.PierceHitFx);
                });
                return true;
            }

            case AbilityKind.HunterMode:
            case AbilityKind.ShadowForm:
            {
                float reach = ability.Kind == AbilityKind.HunterMode ? LaneSettings.MaxAttackRange.Centimeters : 700f;
                if (!duel || distance > reach || c.Now < c.Mob.BoostUntil) return false;
                float boost = ability.Kind == AbilityKind.HunterMode ? 0.25f : (1f + ability.Damage / 200f) * (1f + ability.Heal / 100f) - 1f;
                Boost(c, boost, ability.Seconds);
                Aura(c, ability.Fx, ability.Seconds);
                return true;
            }

            case AbilityKind.Summon:
            {
                bool worth = duel || IsStructure(target) || enemies.Count(e => !IsStructure(e) && Flat(e.Position, c.Position) <= 1200f) >= 3;
                if (!worth || distance > 1600f) return false;
                HeroStats stats = StatsOf(c);
                MobKind kind = SummonKinds.Of(ability with { Damage = HeroResources.AbilityDamage(ability, stats), Heal = HeroResources.AbilityHeal(ability, stats) }, c.Kind.Faction);
                Mob pet = c.Director.SpawnHelper(kind, c.Mob, Toward(c.Position, target.Position, 200f), c.Now, TimeSpan.FromSeconds(ability.Seconds),
                    ability.Key == "golem" ? 1 : 3);
                if (pet.Npc is null) return false;
                Fx(c, null, 1, ability);
                return true;
            }

            case AbilityKind.Ironhead:
                if (!duel || distance > 700f) return false;
                return Ironhead(c, ability);

            case AbilityKind.Barrel:
            {
                if (!duel || distance > MathF.Min(ability.Range, LaneSettings.MaxAttackRange.Centimeters)) return false;
                Position spot = target.Position;
                Fx(c, null, 1, ability);
                After(c, TimeSpan.FromSeconds(Math.Clamp(distance / 800f, 0.3f, 1.6f)), cc =>
                {
                    cc.Director.GroundEffect(cc.Mob, spot, ability.Fx);
                    foreach (Combatant enemy in Enemies(cc))
                        if (!IsStructure(enemy) && Flat(enemy.Position, spot) <= ability.Radius) cc.Director.Immobilize(enemy.Character, cc.Now + TimeSpan.FromSeconds(ability.Seconds));
                });
                return true;
            }

            case AbilityKind.Spin:
            {
                int around = enemies.Count(e => !IsStructure(e) && Flat(e.Position, c.Position) <= ability.Radius);
                if (!((duel && distance <= ability.Radius) || around >= 3)) return false;
                Aura(c, ability.Fx, ability.Seconds + 0.2f);
                After(c, TimeSpan.FromMilliseconds(640), cc => Sweep(cc, ability));
                return true;
            }

            case AbilityKind.Pull:
            {
                if (!duel || distance > ability.Radius || distance < 350f) return false;
                float dx = target.Position.X - c.Position.X, dz = target.Position.Z - c.Position.Z, flat = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
                dx /= flat; dz /= flat;
                int index = 0;
                foreach (Combatant enemy in enemies)
                {
                    if (IsStructure(enemy) || Flat(enemy.Position, c.Position) > ability.Radius) continue;
                    float side = (index++ % 5 - 2) * 90f;
                    var to = new Position(c.Position.X + dx * 170f - dz * side, c.Position.Y, c.Position.Z + dz * 170f + dx * side);
                    c.Director.Pull(enemy.Character, to, TimeSpan.FromSeconds(ability.Seconds));
                }
                Fx(c, null, 1, ability);
                return true;
            }

            case AbilityKind.Execute:
            {
                if (!duel || distance > ability.Range || distance < 250f) return false;
                (int hp, int max) = HealthOf(target);
                int damage = HeroResources.AbilityDamage(ability, StatsOf(c));
                if (!(hp <= damage * 1.5f || (float)hp / max <= 0.5f || distance > 450f)) return false;
                Teleport(c, Toward(target.Position, c.Position, 120f));
                Aura(c, ability.Fx, ability.Seconds + 0.6f);
                TimeSpan impact = TimeSpan.FromSeconds(ability.Seconds * HeroBuffs.ExecuteImpact);
                Hurt(c, target, ability, impact);
                After(c, impact + TimeSpan.FromMilliseconds(250), cc =>
                {
                    if (!Alive(target)) cc.Get<ChampionState>().Ready[slot] = cc.Now;
                });
                return true;
            }

            case AbilityKind.BeliarTongue:
            {
                if (!duel || distance > ability.Range + ability.Radius * 0.5f) return false;
                Position spot = distance <= ability.Range ? target.Position : Toward(c.Position, target.Position, ability.Range);
                c.Director.GroundEffect(c.Mob, spot, ability.Fx);
                float stun = ability.StunSeconds > 0 ? ability.StunSeconds : 1.5f;
                After(c, TimeSpan.FromSeconds(ability.Seconds), cc =>
                {
                    foreach (Combatant enemy in Enemies(cc))
                    {
                        if (IsStructure(enemy) || Flat(enemy.Position, spot) > ability.Radius) continue;
                        cc.Director.Immobilize(enemy.Character, cc.Now + TimeSpan.FromSeconds(stun));
                        Fx(cc, enemy.Character, 2, AbilityService.HandsFx);
                    }
                });
                return true;
            }

            case AbilityKind.Flank:
                if (!duel || distance > ability.Range || distance < 300f) return false;
                Fx(c, null, 1, ability);
                Teleport(c, Toward(target.Position, c.Position, -130f));
                return true;

            case AbilityKind.BeliarMight:
            {
                if (!duel || distance > ability.Range) return false;
                Position spot = target.Position;
                Teleport(c, Toward(spot, c.Position, 150f));
                TimeSpan impact = TimeSpan.FromSeconds(ability.Seconds * HeroBuffs.ExecuteImpact);
                int damage = Boosted(c, HeroResources.AbilityDamage(ability, StatsOf(c)));
                bool magic = HeroResources.IsMagicAbility(hero, ability);
                After(c, impact, cc =>
                {
                    cc.Director.GroundEffect(cc.Mob, spot, ability.Fx);
                    foreach (Combatant enemy in Enemies(cc))
                        if (!IsStructure(enemy) && Flat(enemy.Position, spot) <= ability.Radius) cc.Director.ScheduleDamage(enemy.Character, damage, cc.Now, cc.Npc, magic);
                });
                return true;
            }
        }
        return false;
    }

    private static float MeleeReach(MobContext c) => MathF.Max(c.Kind.AttackRange.Centimeters + 200f, 380f);

    private static bool IsStructure(Combatant who) => who.Mob is { Kind.Immobile: true };

    private static bool Alive(Combatant who) =>
        who.Character is { IsValid: true, IsDead: false } && who.Mob is not { State: MobState.Dead };

    private static (int Health, int Max) HealthOf(Combatant who) => who.Character switch
    {
        Player player => (player.Health, Math.Max(1, player.MaxHealth)),
        _ => (who.Mob?.LastHealth ?? 0, Math.Max(1, who.Mob?.LastMaxHealth ?? 1)),
    };

    private static float Flat(Position a, Position b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static Position Toward(Position from, Position to, float distance)
    {
        float dx = to.X - from.X, dz = to.Z - from.Z, flat = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        return new Position(from.X + dx / flat * distance, from.Y, from.Z + dz / flat * distance);
    }

    private static void Teleport(MobContext c, Position end)
    {
        c.Npc.Position = end;
        c.Mob.LastPosition = end;
    }

    private static void Aura(MobContext c, string fx, float seconds) => c.Director.ChampionAura(c.Mob, fx, (int)(seconds * 1000));

    private static void Boost(MobContext c, float amount, float seconds)
    {
        c.Mob.Boost = amount;
        c.Mob.BoostUntil = c.Now + TimeSpan.FromSeconds(seconds);
    }

    private static int Boosted(MobContext c, int amount) => c.Now < c.Mob.BoostUntil ? (int)Math.Round(amount * (1f + c.Mob.Boost)) : amount;

    private static bool StartHaste(MobContext c, AbilityDef ability)
    {
        if (c.Now < c.Mob.HasteUntil) return false;
        c.Mob.Haste = ability.Damage / 100f;
        c.Mob.HasteUntil = c.Now + TimeSpan.FromSeconds(ability.Seconds);
        Aura(c, ability.Fx, ability.Seconds);
        return true;
    }

    private bool Rampage(MobContext c, AbilityDef ability)
    {
        Boost(c, ability.Damage / 100f, ability.Seconds);
        c.Director.Heal(c.Mob, (int)Math.Round(c.Mob.MaxHealth * ability.Heal / 100f));
        Aura(c, ability.Fx, ability.Seconds);
        return true;
    }

    private static bool Ironhead(MobContext c, AbilityDef ability)
    {
        if (c.Now < c.Mob.CcImmuneUntil) return false;
        c.Mob.CcImmuneUntil = c.Now + TimeSpan.FromSeconds(ability.Seconds);
        c.Mob.StunnedUntil = default;
        Aura(c, ability.Fx, ability.Seconds);
        return true;
    }

    private bool CastDecoy(MobContext c, AbilityDef ability)
    {
        if ((c.Kind.Look ?? hero.Look) is not { } look || c.Mob.Lane is not { } lane) return false;
        Position[] route = lane.RouteOf(c.Kind.Faction);
        Position runTo = ChampionTactics.PointAt(route, ChampionTactics.Progress(route, c.Position) - 3000f, 0f);
        MobKind kind = SummonKinds.CopyOf(hero, c.Kind.Faction, look, new GothicOnline.Api.Scale3(1f, 1f, 1f));
        Mob copy = c.Director.SpawnHelper(kind, c.Mob, c.Position, c.Now, TimeSpan.FromSeconds(ability.Seconds), limit: 1, runTo, c.Kind.MeleeMode);
        if (copy.Npc is null) return false;
        Fx(c, null, 1, ability);
        return true;
    }

    private void Sweep(MobContext c, AbilityDef ability)
    {
        int damage = Boosted(c, HeroResources.AbilityDamage(ability, StatsOf(c)));
        bool magic = HeroResources.IsMagicAbility(hero, ability);
        float bladeFrom = ability.Radius * HeroBuffs.SpinBladeFrom;
        int bladeHits = 0;
        foreach (Combatant enemy in Enemies(c))
        {
            float distance = Flat(enemy.Position, c.Position);
            if (IsStructure(enemy) || distance > ability.Radius) continue;
            bool blade = distance >= bladeFrom;
            c.Director.ScheduleDamage(enemy.Character, blade ? damage : (int)Math.Round(damage * HeroBuffs.SpinShaftDamage), c.Now, c.Npc, magic);
            if (blade && (enemy.IsPlayer || enemy.Mob is { Champion: true } || enemy.Mob?.Lane is null)) bladeHits++;
        }
        int missing = c.Mob.MaxHealth - c.State.Health;
        int heal = (int)Math.Round(missing * HeroBuffs.SpinHeal[Math.Min(bladeHits, HeroBuffs.SpinHeal.Length - 1)]);
        if (heal > 0) c.Director.Heal(c.Mob, heal);
    }

    private static void Fx(MobContext c, Character? target, int kind, AbilityDef ability) => Fx(c, target, kind, ability.Fx);

    private static void Fx(MobContext c, Character? target, int kind, string fx) { if (fx.Length > 0) c.Director.ChampionEffect(c.Mob, target, kind, fx); }

    private HeroStats StatsOf(MobContext c) => c.Mob.ChampionStats ?? HeroResources.StatsOf(hero, 1);

    private int Hurt(MobContext c, Combatant target, AbilityDef ability, TimeSpan delay)
    {
        int amount = Boosted(c, HeroResources.AbilityDamage(ability, StatsOf(c)));
        if (amount > 0) c.Director.ScheduleDamage(target.Character, amount, c.Now + delay, c.Npc, HeroResources.IsMagicAbility(hero, ability));
        return amount;
    }
}
