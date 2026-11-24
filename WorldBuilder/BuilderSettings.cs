using System.Text.RegularExpressions;

namespace BotArena.WorldBuilder;

public static partial class BuilderSettings
{
    public const int MaxVobs = 20000;
    public const int MaxActionsPerSecond = 15;
    public const int SyncBatch = 40;
    public const float MinScale = 0.1f, MaxScale = 10f;
    public const float MaxCoordinate = 500_000f;

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]{1,90}\.(3DS|MDS)$", RegexOptions.IgnoreCase)]
    private static partial Regex VisualPattern();

    public static bool IsValidVisual(string visual) => VisualPattern().IsMatch(visual);
}
