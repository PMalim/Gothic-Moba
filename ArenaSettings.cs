using BotArena.GameData;

namespace BotArena;

public static class ArenaSettings
{
    public static readonly WorldName World = new("G2OMAPALOL.ZEN");
    public static readonly Position Center = new(7000, 190, 7000);

    public static readonly ItemInstance PlayerSword = GameItems.ITMW_SCHWERT1;
    public static readonly ItemInstance PlayerArmor = GameItems.ITAR_SLD_L;
    public static readonly ItemInstance PlayerBow = GameItems.ITRW_BOW_L_02;
    public static readonly ItemInstance PlayerArrows = GameItems.ITRW_ARROW;
    public const int PlayerArrowCount = 200;
    public const int PlayerDexterity = 150;
    public const int PlayerHealth = 600;
    public static readonly TimeSpan PlayerRespawnDelay = TimeSpan.FromSeconds(5);

    public static readonly Camp[] Camps =
    [
        .. (DrakeSettings.Enabled ? new[] { Camp.At("Jama smoka", DrakeSettings.Pit, Bestiary.Drake) } : []),
        .. (BaronSettings.Enabled ? new[] { Camp.At("Jama barona", BaronSettings.Pit, Bestiary.Baron) } : []),
        .. JungleSettings.BuildCamps(),
    ];

    public static readonly TimeSpan ThinkInterval = TimeSpan.FromMilliseconds(500);
    public static readonly Distance MaxHeightDifference = new(450);
    public static readonly Distance FeudRange = new(900);
    public static readonly Distance AlertRange = new(2200);
    public static readonly Distance StreamRadius = new(15000);
    public const int MaxSummoned = 12;

    public static readonly bool AiViewOnJoin = false;
    public static readonly bool RememberState = true;
    public const string MemoryFile = "gamemodes/BotArena/ai-memory.json";
    public static readonly TimeSpan RestoreBodyWithin = TimeSpan.FromMinutes(2);
}
