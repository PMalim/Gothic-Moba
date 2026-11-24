using GothicOnline.Api;

namespace BotArena.Heroes;

public enum AbilityKind
{
    Strike,
    Nova,
    Rain,
    Dash,
    Heal,
    Shield,
    Empower,
    Haste,
    Curse,
    Rampage,
    Passive,
    Roll,
    PierceShot,
    HunterMode,
    Summon,
    Lightning,
    Decoy,
    Brewer,
    Barrel,
    Ironhead,
    ShadowForm,
    Spin,
    Bloodlust,
    Pull,
    Execute,
    BeliarTongue,
    Servant,
    Flank,
    BeliarMight,
    Ignite,
    TribeProgress,
    Totem,
    TribeGuard,
}

public sealed record AbilityDef(string Key, string Name, string Text, AbilityKind Kind, float Cooldown, float Range, float Radius,
    int Damage, int Heal, float Seconds, string Fx)
{
    public bool IsPassive => Kind is AbilityKind.Passive or AbilityKind.Lightning or AbilityKind.Brewer or AbilityKind.Servant;

    public float AdRatio { get; init; }
    public float ApRatio { get; init; }
    public float HealAdRatio { get; init; }
    public float HealApRatio { get; init; }
    public bool? Magic { get; init; }
    public float StunSeconds { get; init; }
}

public enum HeroWeapon { OneHanded, TwoHanded, Bow, None }

public sealed record HeroDef(string Key, string Name, string Title, string Role, string Lore, int Price, int Health, int Strength, int Dexterity,
    HeroWeapon WeaponClass, ItemInstance? Weapon, ItemInstance? Armor, ItemInstance? Ammo, AbilityDef[] Abilities)
{
    public NpcInstance? Instance { get; init; }
    public PlayerVisual? Look { get; init; }
    public int AmmoCount { get; init; } = 300;
    public string[] Overlays { get; init; } = [];
    public ItemInstance? Rune { get; init; }
    public int MagicCircle { get; init; } = 6;
    public int GameMana { get; init; } = 999999;
    public int? SkillMana { get; init; }
    public int? WeaponMana { get; init; }
    public bool Bleeds { get; init; }
}
