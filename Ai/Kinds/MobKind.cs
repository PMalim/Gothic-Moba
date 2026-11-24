namespace BotArena.Ai.Kinds;

public enum Faction { Bandits, Beasts, Prey, Undead, Orcs, Blue, Red }

public enum StructureKind { None, Turret, Inhibitor, Nexus, Dragon }

public sealed record MobKind
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required NpcInstance Instance { get; init; }
    public required Faction Faction { get; init; }
    public required MobBehaviour Behaviour { get; init; }
    public bool Humanoid { get; init; }

    public MinionRole? Role { get; init; }
    public int Health { get; init; } = 100;
    public int Strength { get; init; } = 30;
    public int Dexterity { get; init; } = 10;
    public int Skill { get; init; } = 30;
    public ItemInstance? Armor { get; init; }
    public GothicOnline.Api.PlayerVisual? Look { get; init; }
    public float Scale { get; init; } = 1f;
    public ItemInstance? Rune { get; init; }
    public int Mana { get; init; } = 100;
    public int ArmorValue { get; init; }
    public int ResistValue { get; init; }
    public int? Damage { get; init; }
    public float Radius { get; init; }
    public bool Immobile { get; init; }
    public StructureKind Structure { get; init; }
    public bool Respawns { get; init; }
    public ItemInstance? MeleeWeapon { get; init; }
    public ItemInstance? RangedWeapon { get; init; }
    public ItemInstance? Ammo { get; init; }
    public WeaponMode MeleeMode { get; init; } = WeaponMode.Fists;

    public Distance SightRange { get; init; } = new(1500);
    public Distance AttackRange { get; init; } = new(200);
    public TimeSpan AttackCooldown { get; init; } = TimeSpan.FromMilliseconds(1300);
    public MeleeAttack[] Attacks { get; init; } = [MeleeAttack.Forward];
    public int Combo { get; init; } = 1;
    public TimeSpan RespawnDelay { get; init; } = TimeSpan.FromSeconds(25);
    public TimeSpan? FirstSpawn { get; init; }
    public int? Gold { get; init; }
    public int? Xp { get; init; }
    public JungleBuff Buff { get; init; }
}

public enum JungleBuff { None, Blue, Red }

public static class Factions
{
    public static bool Hostile(Faction a, Faction b) => (a, b) switch
    {
        (Faction.Blue, Faction.Red) or (Faction.Red, Faction.Blue) => true,
        (Faction.Blue or Faction.Red, _) or (_, Faction.Blue or Faction.Red) => false,
        (Faction.Undead, not Faction.Undead) => true,
        (not Faction.Undead, Faction.Undead) => true,
        (Faction.Orcs, Faction.Bandits) or (Faction.Bandits, Faction.Orcs) => true,
        (Faction.Beasts, Faction.Prey) => true,
        _ => false,
    };
}
