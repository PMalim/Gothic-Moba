using BotArena.Ai.Kinds;

namespace BotArena;

public static class JungleSettings
{
    public static readonly bool Enabled = true;

    public const float LeashCm = 1100f;
    public const float ReturnRegen = 0.4f;
    public const float ResetRegen = 0.25f;

    public const float GroundLift = -40f;

    public const string BlueKey = "buff_blue", RedKey = "buff_red";
    public const int BuffSeconds = 120;
    public const int BlueManaRegen = 200;
    public const int BurnSeconds = 3, BurnDamage = 7;
    public const string BurnFx = "spellFX_ItemLeapTrail";

    public static readonly TimeSpan FirstSpawn = TimeSpan.FromSeconds(90), BuffRespawn = TimeSpan.FromMinutes(5), CampRespawn = TimeSpan.FromSeconds(135);

    public static Camp[] BuildCamps()
    {
        if (!Enabled) return [];
        return
        [
            Placed("Lodowy Golem (niebiescy)", [(3268, 129, -676, Bestiary.JungleGolem)]),
            Placed("Cieniostwór (niebiescy)", [(-136, 129, 5965, Bestiary.JungleShadowbeast)]),
            At("Golemy (niebiescy)", 8624, 129, -1569, Bestiary.JungleStoneGolem, Bestiary.JungleStoneGolemSmall),
            Placed("Zębacze (niebiescy)", [(2258, 129, 7967, Bestiary.JungleSnapper), (2247, 129, 8197, Bestiary.JungleSnapperSmall)]),
            At("Wilki (niebiescy)", 6523, 131, 1948, Bestiary.JungleWarg, Bestiary.JungleWolf, Bestiary.JungleWolf),
            At("Widma (niebiescy)", 923, 131, 4048, Bestiary.JungleScavenger, Bestiary.JungleScavengerSmall, Bestiary.JungleScavengerSmall, Bestiary.JungleScavengerSmall),
            Placed("Lodowy Golem (czerwoni)", [(-5997, 279, 366, Bestiary.JungleGolem)]),
            Placed("Cieniostwór (czerwoni)", [(988, 279, -6021, Bestiary.JungleShadowbeast)]),
            At("Zębacze (czerwoni)", -7929, 279, 1789, Bestiary.JungleSnapper, Bestiary.JungleSnapperSmall),
            Placed("Golemy (czerwoni)", [(2105, 276, -8346, Bestiary.JungleStoneGolem), (2278, 278, -8342, Bestiary.JungleStoneGolemSmall)]),
            At("Wilki (czerwoni)", -3977, 280, -1552, Bestiary.JungleWarg, Bestiary.JungleWolf, Bestiary.JungleWolf),
            At("Widma (czerwoni)", 1007, 154, -3102, Bestiary.JungleScavenger, Bestiary.JungleScavengerSmall, Bestiary.JungleScavengerSmall, Bestiary.JungleScavengerSmall),
        ];
    }

    private static Camp Placed(string name, (float X, float Y, float Z, MobKind Kind)[] members)
    {
        Position[] spots = [.. members.Select(m => new Position(m.X, m.Y + GroundLift, m.Z))];
        return Timed(Camp.At(name, spots[0], [.. members.Select(m => m.Kind)]) with { Spots = spots });
    }

    private static Camp At(string name, float x, float ground, float z, params MobKind[] members) =>
        Timed(Camp.At(name, new Position(x, ground + GroundLift, z), members));

    private static Camp Timed(Camp camp) => camp with
    {
        FirstSpawn = FirstSpawn,
        Respawn = camp.Members.Any(k => k.Buff != JungleBuff.None) ? BuffRespawn : CampRespawn,
    };
}
