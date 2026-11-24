namespace BotArena.Heroes;

public static class HeroBuffs
{
    public const float StackDamage = 2f;
    public const float RampageStackFactor = 1.5f;
    public const float RampageScale = 1.3f;
    public const float RampageHealth = 0.5f;
    public static readonly TimeSpan CurseTick = TimeSpan.FromMilliseconds(500);
    public const string EmpowerHitFx = "spellFX_LeeAxeHit";
    public static readonly TimeSpan PierceWindow = TimeSpan.FromSeconds(5);
    public const float HuntPierceStun = 2f;
    public const float HuntComboFactor = 2f;
    public static readonly TimeSpan HuntInvisibleAfter = TimeSpan.FromMilliseconds(500);
    public const string PierceHitFx = "spellFX_DiegoPierceHit";
    public const string PierceAnimation = "T_BOWRELOAD";
    public const string SpinAnimation = "T_DANCE_08", PullAnimation = "T_DANCE_07", ExecuteAnimation = "T_DANCE_06";
    public const float SpinBladeFrom = 0.6f, SpinShaftDamage = 0.35f;
    public static readonly float[] SpinHeal = [0f, 0.17f, 0.34f, 0.51f];

    public const int BleedMaxStacks = 5;
    public const float BleedSeconds = 5f, BleedBase = 10f, BleedPerLevel = 2f, BleedAdRatio = 0.12f;
    public const string BleedFx = "spellFX_ThorusBlood";

    public const float ExecuteSeconds = 1.28f, ExecuteImpact = 21f / 32f;

    public const string RavenLeapAnimation = "T_DANCE_05";
    public const float RavenLeapSeconds = 36f / 25f, RavenLeapImpact = 24f / 36f;

    public const int IgniteCharges = 3;
    public const float IgniteSeconds = 3f, IgniteTick = 0.5f;
    public const string IgniteSelfFx = "spellFX_ItemLeapTrail";

    public const int TribeMaxStacks = 40, TribeMobStacks = 1, TribeChampionStacks = 5;
    public const float TribeMaxShare = 35f;
    public const float TribeRange = 700f;

    public const float TotemDistance = 250f, TotemInterval = 1f;
    public const string TotemVisual = "OW_ORCS_TOTEM_02.3DS", TotemShotFx = "spellFX_InstantFireball";

    public const float GuardMaxSeconds = 3f;
    public const string GuardSkullVisual = "SKULL.3DS";
}

public sealed class BuffState
{
    public bool Primed;
    public TimeSpan PrimedUntil;
    public string PrimedFx = "";
    public int PrimedDamage;
    public TimeSpan HasteUntil;
    public TimeSpan RampageUntil;
    public int RampageBaseHealth;
    public float RampageDamage;
    public string RampageFx = "";
    public string HasteFx = "";
    public bool Pierce;
    public TimeSpan PierceUntil;
    public string PierceFx = "";
    public float PierceStun;
    public TimeSpan HuntUntil;
    public string HuntFx = "";
    public TimeSpan InvisibleUntil;
    public Character? ComboTarget;
    public int ComboCount;
    public TimeSpan ComboAt;
    public bool Hunting(TimeSpan now) => now < HuntUntil;
    public TimeSpan NextBeer;
    public TimeSpan LastCombat;
    public TimeSpan IronUntil;
    public string IronFx = "";
    public TimeSpan ShadowUntil;
    public NpcInstance? ShadowOwnInstance;
    public float ShadowBonus;
    public int LastNormalHit;
    public TimeSpan BloodUntil;
    public int BloodDrain;
    public string BloodFx = "";
    public int BleedStacks;
    public TimeSpan BleedUntil;
    public int FireCharges;
    public TimeSpan FireUntil;
    public int FireDamage;
    public string FireFx = "";
    public int TribeStacks;
    public bool Shadowed(TimeSpan now) => ShadowOwnInstance is not null && now < ShadowUntil;
    public bool InRampage(TimeSpan now) => RampageBaseHealth > 0 && now < RampageUntil;
}

public sealed class CurseZone(BotArena.Matches.Match match, Player caster, Faction team, Position at, float radius, int damage, TimeSpan until)
{
    public BotArena.Matches.Match Match { get; } = match;
    public Player Caster { get; } = caster;
    public Faction Team { get; } = team;
    public Position At { get; } = at;
    public float Radius { get; } = radius;
    public int Damage { get; } = damage;
    public TimeSpan Until { get; } = until;
    public TimeSpan NextTick;
}
