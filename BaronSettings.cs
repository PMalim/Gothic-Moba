namespace BotArena;

public static class BaronSettings
{
    public static readonly bool Enabled = true;

    public static readonly Position Pit = new(5258, 48, -3408);
    public const float PitAngle = 218f;

    public static readonly TimeSpan FirstSpawn = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Respawn = TimeSpan.FromMinutes(6);

    public const int Health = 17792;
    public const int AttackDamage = 350;
    public static readonly TimeSpan AttackCooldown = TimeSpan.FromMilliseconds(1600);
    public static readonly Distance AttackRange = new(1500);
    public const int Armor = 104;
    public const int MagicResist = 67;
    public const float Scale = 1f;
    public static readonly float Radius = 170f;
    public static readonly Distance Aggro = new(1500);
    public static readonly TimeSpan ResetAfter = TimeSpan.FromSeconds(8);
    public const float ResetRegen = 0.2f;

    public static readonly Distance MeleeRange = new(400);
    public static readonly Distance MeleeSplash = new(450);
    public const float MeleeSplashShare = 0.5f;
    public const float OthersShare = 0.5f;
    public static readonly Distance AggroSlack = new(150);
    public const float CorrosionShare = 0.35f;
    public const string CorrosionFx = "spellFX_Fear";
    public const string BoltFx = "spellFX_Bolt";
    public static readonly float BoltSpeed = 2200f;
    public const int AbilityEvery = 6;

    public static readonly TimeSpan AcidPoolWindup = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan AcidPoolField = TimeSpan.FromMilliseconds(2500);
    public static readonly Distance AcidPoolRange = new(1400);
    public static readonly Distance AcidPoolRadius = new(260);
    public const float AcidPoolSlow = 0.6f;
    public const string AcidPoolFx = "spellFX_Fear_Ground";

    public static readonly TimeSpan AcidShotWindup = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan AcidShotStream = TimeSpan.FromSeconds(2);
    public static readonly Distance AcidShotLength = new(1500);
    public static readonly Distance AcidShotWidth = new(180);

    public static readonly TimeSpan QuakeDelay = TimeSpan.FromMilliseconds(1250);
    public static readonly Distance QuakeRadius = new(300);
    public const string QuakeFx = "spellFX_Fear_Ground";

    public static readonly TimeSpan HuntingFirst = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan HuntingEvery = TimeSpan.FromMilliseconds(100);
    public const float HuntingShare = 0.2f;
    public const string HuntingFx = "spellFX_LightningFlash_HEAVENSRAGE";

    public static readonly TimeSpan TerritorialLean = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan TerritorialAim = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan TerritorialPull = TimeSpan.FromMilliseconds(330);
    public static readonly Distance TerritorialRange = new(1700);
    public const float TerritorialHalfAngle = 40f;
    public const float TerritorialShare = 1.4f;
    public static readonly float CageSeconds = 1.5f;
    public const string CageFx = "spellFX_ItemChainsGround";
    public const string CageHitFx = "spellFX_Fear";

    public static readonly Distance RiftRange = new(3500);
    public static readonly TimeSpan RiftLength = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan RiftEvery = TimeSpan.FromMilliseconds(166);
    public const int RiftFirst = 150, RiftTick = 50, RiftTicks = 18;
    public const string RiftFx = "spellFX_Fear_Ground";

    public const int MaxCorruption = 100;
    public const float CorruptionPerStack = 0.5f;
    public static readonly TimeSpan CorruptionLength = TimeSpan.FromSeconds(8);

    public const float GazeReduction = 0.5f;
    public static readonly TimeSpan GazeLength = TimeSpan.FromSeconds(8);

    public const string AttackAnimation = "T_BARON_ATTACK", CastAnimation = "T_BARON_CAST";
    public static readonly TimeSpan AttackAnimLength = TimeSpan.FromMilliseconds(1200), CastAnimLength = TimeSpan.FromMilliseconds(2400);
    public static readonly TimeSpan BoltRelease = TimeSpan.FromMilliseconds(560);
    public static readonly TimeSpan CastBusy = TimeSpan.FromMilliseconds(500);

    public const int TeamGold = 150;
    public const int KillerGold = 100;
    public const int TeamXp = 650;

    public const string BuffKey = "buff_baron";
    public const int BuffSeconds = 180;
    public static (int Ad, int Ap) BuffAt(TimeSpan gameTime)
    {
        double f = Math.Clamp((gameTime.TotalMinutes - FirstSpawn.TotalMinutes) / FirstSpawn.TotalMinutes, 0, 1);
        return ((int)Math.Round(12 + 36 * f), (int)Math.Round(20 + 60 * f));
    }
    public const float EmpoweredDamage = 1.35f;
    public const float EmpoweredResist = 0.6f;
}
