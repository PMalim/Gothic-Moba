namespace BotArena;

public static class DrakeSettings
{
    public static readonly bool Enabled = true;
    public static readonly Position Pit = new(-2277, 38, 5948);
    public const float PitAngle = 210f;
    public static readonly Distance Aggro = new(1000);

    public const int Health = 3500;
    public const float Scale = 1f;
    public static readonly TimeSpan Respawn = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan FirstSpawn = TimeSpan.FromMinutes(5);

    public static readonly Distance BiteRange = new(420);
    public const int BiteDamage = 70;
    public static readonly TimeSpan BiteCooldown = TimeSpan.FromMilliseconds(1700);
    public static readonly TimeSpan BiteHit = TimeSpan.FromMilliseconds(600);
    public static readonly TimeSpan BiteLength = TimeSpan.FromMilliseconds(1200);

    public static readonly Distance SpitRange = new(1100);
    public const int SpitDamage = 55;
    public const float SpitSpeed = 1800f;
    public static readonly TimeSpan SpitCooldown = TimeSpan.FromMilliseconds(2000);
    public static readonly TimeSpan SpitRelease = TimeSpan.FromMilliseconds(520);
    public static readonly TimeSpan SpitLength = TimeSpan.FromMilliseconds(1120);
    public const string SpitFx = "spellFX_InstantFireball";

    public static readonly Distance BreathRange = new(1000);
    public const float BreathHalfAngle = 40f;
    public const int BreathDamagePerTick = 45;
    public const int BreathTicks = 4;
    public static readonly TimeSpan BreathFirstTick = TimeSpan.FromMilliseconds(760);
    public static readonly TimeSpan BreathTickEvery = TimeSpan.FromMilliseconds(420);
    public static readonly TimeSpan BreathLength = TimeSpan.FromMilliseconds(3000);
    public static readonly TimeSpan BreathCooldown = TimeSpan.FromSeconds(13);
    public static readonly TimeSpan BreathFirstDelay = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan RoarLength = TimeSpan.FromMilliseconds(2000);
    public static readonly Distance HopTrigger = new(150);
    public static readonly TimeSpan HopCooldown = TimeSpan.FromSeconds(9);
    public static readonly TimeSpan HopLength = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan LandLength = TimeSpan.FromMilliseconds(2560);

    public static readonly Distance Leash = new(1200);
    public const float ResetRegen = 0.25f;
    public const float ReturnRegen = 0.4f;

    public const int Gold = 150;
    public const int Xp = 200;
    public const int BuffAd = 8;
    public const int BuffAp = 12;
    public const int MaxStacks = 4;
}
