namespace BotArena.Heroes;

public readonly record struct HeroStats(int Ad, int Ap, int Crit, int Armor, int Resist, int Cdr, float Regen);

public readonly record struct HeroGrowth(float Hp, float HpG, float Mana, float ManaG, float HpRegen, float HpRegenG, float ManaRegen, float ManaRegenG,
    float Ad, float AdG, float Ap, float ApG, float Armor, float ArmorG, float Resist, float ResistG);

public readonly record struct BasicAttackInfo(int Damage, bool Magic);

public static class HeroResources
{
    public const int MaxLevel = 12;

    public const float LevelStretch = 17f / (MaxLevel - 1);

    public const int MaxRank = 3;

    public static bool Rankable(HeroDef hero, int slot) => slot >= 0 && slot < hero.Abilities.Length;

    public static bool IsUltimate(HeroDef hero, int slot) =>
        hero.Abilities.Length >= 4 && slot == hero.Abilities.Length - 1 && Rankable(hero, slot) && !hero.Abilities[slot].IsPassive;

    public static int RequiredLevel(HeroDef hero, int slot, int rank) => IsUltimate(hero, slot) ? 1 + rank * 3 : rank * 2 - 1;

    public static int SkillPoints(int level, IReadOnlyList<int> ranks) => Math.Max(0, level - ranks.Sum());

    public static bool CanRank(HeroDef hero, int slot, int level, IReadOnlyList<int> ranks) =>
        Rankable(hero, slot) && ranks[slot] < MaxRank && SkillPoints(level, ranks) > 0 && level >= RequiredLevel(hero, slot, ranks[slot] + 1);

    public static AbilityDef AtRank(AbilityDef a, int rank)
    {
        int up = Math.Clamp(rank, 1, MaxRank) - 1;
        if (up == 0) return a;
        switch (a.Kind)
        {
            case AbilityKind.Passive: return a with { Damage = a.Damage + 25 * up };
            case AbilityKind.Lightning: return a with { Damage = a.Damage + 15 * up };
            case AbilityKind.Brewer: return a with { Heal = a.Heal + 5 * up, Seconds = MathF.Max(10f, a.Seconds - 5f * up) };
        }
        float damage = 1f + 0.3f * up;
        AbilityDef r = a with { Cooldown = MathF.Round(a.Cooldown * (1f - 0.08f * up), 1) };
        return a.Kind switch
        {
            AbilityKind.Strike or AbilityKind.Rain or AbilityKind.Dash or AbilityKind.Spin or AbilityKind.Execute or AbilityKind.Pull =>
                r with { Damage = (int)MathF.Round(a.Damage * damage) },
            AbilityKind.Nova => r with { Damage = (int)MathF.Round(a.Damage * damage), Seconds = a.Seconds > 0 ? a.Seconds + 0.2f * up : 0 },
            AbilityKind.Curse => r with { Damage = (int)MathF.Round(a.Damage * (1f + 0.4f * up)), Seconds = a.Seconds + 0.5f * up },
            AbilityKind.Heal => r with { Heal = (int)MathF.Round(a.Heal * damage) },
            AbilityKind.Shield => r with { Damage = Math.Min(85, a.Damage + 5 * up), Seconds = a.Seconds + 0.5f * up },
            AbilityKind.Haste => r with { Damage = a.Damage + 5 * up, Seconds = a.Seconds + 0.5f * up },
            AbilityKind.Empower => r with { Damage = (int)MathF.Round(a.Damage * damage), Seconds = a.Seconds + 0.5f * up },
            AbilityKind.Rampage => r with { Damage = a.Damage + 5 * up, Heal = a.Heal + 5 * up, Seconds = a.Seconds + up, Cooldown = MathF.Max(20f, a.Cooldown - 5f * up) },
            AbilityKind.PierceShot => r with { Seconds = a.Seconds + 0.25f * up },
            AbilityKind.HunterMode => r with { Seconds = a.Seconds + 2f * up },
            AbilityKind.Summon => r with { Damage = (int)MathF.Round(a.Damage * damage), Heal = (int)MathF.Round(a.Heal * damage), Seconds = a.Seconds + up },
            AbilityKind.Decoy or AbilityKind.Ironhead => r with { Seconds = a.Seconds + 0.5f * up },
            AbilityKind.ShadowForm => r with { Damage = a.Damage + 5 * up, Seconds = a.Seconds + up },
            AbilityKind.Barrel => r with { Seconds = a.Seconds + 0.2f * up },
            AbilityKind.Bloodlust => r with { Damage = (int)MathF.Round(a.Damage * damage) },
            AbilityKind.BeliarTongue => r with { StunSeconds = a.StunSeconds + 0.25f * up },
            AbilityKind.BeliarMight => r with { Damage = (int)MathF.Round(a.Damage * damage), Cooldown = MathF.Round(a.Cooldown * (1f - 0.1f * up), 1) },
            AbilityKind.Ignite => r with { Damage = (int)MathF.Round(a.Damage * damage), Seconds = a.Seconds + 1f * up },
            AbilityKind.TribeProgress => r with { Damage = a.Damage + 2 * up, Range = a.Range + 100f * up },
            AbilityKind.Totem => r with { Damage = (int)MathF.Round(a.Damage * damage), Seconds = a.Seconds + up },
            AbilityKind.TribeGuard => r with { Seconds = MathF.Min(HeroBuffs.GuardMaxSeconds, a.Seconds + 0.5f * up), Cooldown = MathF.Max(40f, MathF.Round(a.Cooldown * (1f - 0.1f * up), 1)) },
            AbilityKind.Servant => r with { Damage = (int)MathF.Round(a.Damage * damage), Heal = (int)MathF.Round(a.Heal * damage), Seconds = a.Seconds + 0.5f * up },
            _ => r,
        };
    }

    public static int ManaCost(AbilityDef baseAbility, int rank) =>
        baseAbility.IsPassive ? 0 : (int)(Math.Round(ManaCost(baseAbility) * (1f + 0.1f * (Math.Clamp(rank, 1, MaxRank) - 1)) / 5.0) * 5);

    private static int BaseMana(HeroDef hero) => hero.SkillMana ?? hero.Role switch { "Mag" => 520, "Wsparcie" => 460, "Strzelec" => 380, _ => 320 };

    public static int MaxMana(HeroDef hero, int level = 1) { HeroGrowth g = GrowthOf(hero); return (int)Math.Round(Grow(g.Mana, g.ManaG, level)); }

    public static float ManaRegen(HeroDef hero, int level = 1) { HeroGrowth g = GrowthOf(hero); return Grow(g.ManaRegen, g.ManaRegenG, level) / 5f; }

    public static int MaxHealth(HeroDef hero, int level = 1) { HeroGrowth g = GrowthOf(hero); return (int)Math.Round(Grow(g.Hp, g.HpG, level)); }

    public static int ManaCost(AbilityDef ability) => ability.IsPassive ? 0 : (int)(Math.Round((20 + ability.Cooldown * 4) / 5.0) * 5);

    public static bool IsMagic(HeroDef hero) => hero.Role is "Mag" or "Wsparcie";

    public const float AdScaling = 0.35f, ApScaling = 0.6f;

    public static int AbilityDamage(AbilityDef a, HeroStats stats) => a.Damage + (int)MathF.Round(a.AdRatio * stats.Ad + a.ApRatio * stats.Ap);

    public static int AbilityHeal(AbilityDef a, HeroStats stats) => a.Heal + (int)MathF.Round(a.HealAdRatio * stats.Ad + a.HealApRatio * stats.Ap);

    public static bool IsMagicAbility(HeroDef hero, AbilityDef a) => a.Magic ?? (a.Kind == AbilityKind.Curse || IsMagic(hero));

    public static string RatioText(float ad, float ap)
    {
        var parts = new List<string>();
        if (ad > 0) parts.Add($"+{Math.Round(ad * 100):0}% AD");
        if (ap > 0) parts.Add($"+{Math.Round(ap * 100):0}% AP");
        return string.Join(", ", parts);
    }

    public const float CritMultiplier = 2.0f;

    public const float CasterBasicAp = 0.55f;

    public const int MaxCdr = 60;

    public static float Grow(float baseValue, float growth, int level)
    {
        float steps = (Math.Clamp(level, 1, MaxLevel) - 1) * LevelStretch;
        return baseValue + growth * steps * (0.7025f + 0.0175f * steps);
    }

    private enum Archetype { Tank, Mage, Marksman, Hybrid }

    private static Archetype ArchetypeOf(HeroDef hero) => hero.Key switch
    {
        "kruk" => Archetype.Hybrid,
        "mag" or "kaplan" or "xardas" or "urshak" => Archetype.Mage,
        "lowca" or "diego" or "zabojca" => Archetype.Marksman,
        _ => Archetype.Tank,
    };

    private static (int Ad, int Ap, int Crit, int Armor, int Resist, int Cdr) BaseCombat(HeroDef hero) => hero.Key switch
    {
        "rycerz" => (70, 0, 5, 45, 30, 0),
        "lowca" => (85, 0, 25, 18, 15, 5),
        "mag" => (40, 90, 0, 12, 25, 10),
        "zabojca" => (90, 0, 20, 22, 18, 5),
        "kaplan" => (45, 70, 0, 28, 35, 10),
        "barbarzynca" => (95, 0, 10, 35, 20, 0),
        "lee" => (80, 0, 8, 40, 25, 5),
        "diego" => (88, 0, 20, 20, 18, 10),
        "xardas" => (40, 100, 5, 18, 40, 10),
        "cipher" => (85, 0, 10, 35, 20, 5),
        "thorus" => (95, 0, 10, 45, 25, 5),
        "kruk" => (90, 45, 8, 40, 28, 5),
        "urshak" => (45, 85, 0, 30, 38, 10),
        _ => (60, 0, 5, 25, 20, 0),
    };

    public static HeroGrowth GrowthOf(HeroDef hero)
    {
        (int ad, int ap, _, int armor, int resist, _) = BaseCombat(hero);
        (float hpG, float manaG, float hpRegG, float mpRegG, float adG, float apG, float armG, float mrG, float hpRegen) = ArchetypeOf(hero) switch
        {
            Archetype.Tank => (0.165f, 0.20f, 0.10f, 0.07f, 0.065f, 0.00f, 0.14f, 0.065f, 9.0f),
            Archetype.Mage => (0.175f, 0.06f, 0.24f, 0.10f, 0.057f, 0.06f, 0.20f, 0.043f, 3.0f),
            Archetype.Hybrid => (0.165f, 0.20f, 0.10f, 0.07f, 0.065f, 0.10f, 0.14f, 0.065f, 9.0f),
            _ => (0.150f, 0.08f, 0.15f, 0.08f, 0.070f, 0.00f, 0.15f, 0.050f, 4.5f),
        };
        float mana = BaseMana(hero);
        float manaRegen = mana * 0.025f;
        return new HeroGrowth(hero.Health, hero.Health * hpG, mana, mana * manaG, hpRegen, hpRegen * hpRegG, manaRegen, manaRegen * mpRegG,
            ad, ad * adG, ap, ap * apG, armor, armor * armG, resist, resist * mrG);
    }

    public static HeroStats StatsOf(HeroDef hero, int level)
    {
        HeroGrowth g = GrowthOf(hero);
        (_, _, int crit, _, _, int cdr) = BaseCombat(hero);
        int grown = Math.Max(0, level - 1);
        return new HeroStats((int)Math.Round(Grow(g.Ad, g.AdG, level)), g.Ap > 0 ? (int)Math.Round(Grow(g.Ap, g.ApG, level)) : 0, crit,
            (int)Math.Round(Grow(g.Armor, g.ArmorG, level)), (int)Math.Round(Grow(g.Resist, g.ResistG, level)),
            Math.Min(MaxCdr, cdr + grown / 2), MathF.Round(Grow(g.HpRegen, g.HpRegenG, level), 1));
    }

    public static float Cooldown(float seconds, int cdr) => seconds * CooldownScale * (1f - Math.Clamp(cdr, 0, MaxCdr) / 100f);

    public const float CooldownScale = 1.5f;

    public static float Mitigation(int defence) => 100f / (100f + Math.Max(0, defence));

    public static BasicAttackInfo BasicAttackOf(HeroDef hero, HeroStats stats)
    {
        if (!IsMagic(hero)) return new(stats.Ad, false);
        return new((int)Math.Round(stats.Ap * CasterBasicAp), true);
    }

    public static int Scaled(HeroDef hero, HeroStats stats, int baseDamage) =>
        baseDamage + (int)MathF.Round(IsMagic(hero) ? stats.Ap * ApScaling : stats.Ad * AdScaling);
}
