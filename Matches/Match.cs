namespace BotArena.Matches;

public enum MatchMode { Bots, Normal, Ranked }

public enum MatchState { Room, Countdown, Running, Finished }

public sealed class DamageStats
{
    public long ToChampions { get; set; }
    public long ToObjectives { get; set; }
    public long ToMinions { get; set; }
    public long Taken { get; set; }
    public long GoldEarned { get; set; }
    public long Total => ToChampions + ToObjectives + ToMinions;
}

public sealed class MatchPlayer(Player player)
{
    public DamageStats Stats { get; } = new();
    public PlayerId Id { get; internal set; } = player.Id;
    public string Uid { get; internal set; } = player.Uid;
    public string Name { get; set; } = player.Name;
    public Faction Team { get; set; } = Faction.Blue;
    public string Hero { get; set; } = "";
    public List<string> Items { get; } = [];
    public Dictionary<string, int> Charges { get; } = [];
    public Dictionary<string, long> Elixirs { get; } = [];
    public int BaronAd { get; set; }
    public int BaronAp { get; set; }
    public bool Ready { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int BotKills { get; set; }
    public int Gold { get; set; } = StartGold;
    public int Xp { get; set; }
    public const int StartGold = 500;
    public static readonly int[] LevelXp = [0, 100, 250, 450, 700, 1000, 1350, 1750, 2200, 2700, 3250, 3850];
    public int[] Ranks { get; } = new int[4];
    public int Level { get { int level = 1; for (int i = 1; i < LevelXp.Length; i++) if (Xp >= LevelXp[i]) level = i + 1; return level; } }
    public int StartLp { get; set; }
    public bool Left { get; set; }
    public List<(ItemInstance Item, int Amount)> Gear { get; } = [];
    public float Stacks { get; set; }
    public GothicOnline.Api.PlayerVisual? OwnLook { get; set; }
    public (int MagicLevel, int MaxMana)? OwnMagic { get; set; }
    public List<string> Overlays { get; } = [];
}

public sealed class MatchBot(int id, Faction team, string hero)
{
    public int Id { get; } = id;
    public Faction Team { get; } = team;
    public string Hero { get; } = hero;
    public DamageStats Stats { get; } = new();
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public bool BaronActive { get; set; }
    public TimeSpan BaronUntil { get; set; }
    public int BaronAd { get; set; }
    public int BaronAp { get; set; }
    public int Cs { get; set; }
    public int Gold { get; set; } = MatchPlayer.StartGold;
    public int Xp { get; set; }
    public int Level { get { int level = 1; for (int i = 1; i < MatchPlayer.LevelXp.Length; i++) if (Xp >= MatchPlayer.LevelXp[i]) level = i + 1; return level; } }
    public List<string> Items { get; } = [];
    public List<string> Build { get; } = [];
    public string Name => $"{Heroes.HeroCatalog.Find(Hero)?.Name ?? Hero} (bot)";
}

public sealed class Match(int id, MatchMode mode, int teamSize, VirtualWorldId world, string title, PlayerId host)
{
    public const int MaxTeamSize = 5;
    public const int PlayerTeamSize = 3;

    public int Id { get; } = id;
    public MatchMode Mode { get; } = mode;
    public MatchState State { get; internal set; } = MatchState.Room;
    public string Title { get; set; } = title;
    public int TeamSize { get; } = Math.Clamp(teamSize, 1, MaxTeamSize);
    public VirtualWorldId World { get; } = world;
    public PlayerId Host { get; internal set; } = host;
    public Dictionary<PlayerId, MatchPlayer> Players { get; } = [];
    public List<MatchBot> Bots { get; } = [];
    internal int NextBotId = 1;
    public Dictionary<Faction, int> Score { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };

    public MobDirector? Director { get; internal set; }
    public WaveSpawner? Waves { get; internal set; }

    internal TimeSpan NextScorePush;
    internal int CensusStage;
    internal TimeSpan NextGoldTick;
    internal TimeSpan NextBotLog;
    public TimeSpan CreatedAt { get; internal set; }
    public TimeSpan StartsAt { get; internal set; }
    public TimeSpan StartedAt { get; internal set; }
    public TimeSpan EndedAt { get; internal set; }
    public Faction? Winner { get; internal set; }
    public Faction? NexusFallen { get; internal set; }
    public Dictionary<Faction, int> TurretsBuilt { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public Dictionary<Faction, int> TurretsDestroyed { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public Dictionary<Faction, int> DrakeStacks { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public Dictionary<Faction, int> DrakeKills { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public Dictionary<Faction, int> BaronKills { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public Dictionary<Faction, int> InhibitorsDestroyed { get; } = new() { [Faction.Blue] = 0, [Faction.Red] = 0 };
    public List<MatchResult> Results { get; } = [];
    internal Dictionary<Mob, Dictionary<string, TimeSpan>> TowerHits { get; } = [];
    public List<ResultRow> Board { get; } = [];

    public bool IsRanked => Mode == MatchMode.Ranked;
    public int MaxPlayers => Mode == MatchMode.Bots ? TeamSize : TeamSize * 2 - Bots.Count;
    public bool IsOpen => State is MatchState.Room or MatchState.Countdown && TeamToJoin() is not null;
    public int Count(Faction team) => Players.Values.Count(p => p.Team == team);
    public int BotCount(Faction team) => Bots.Count(b => b.Team == team);
    public int Total(Faction team) => Count(team) + BotCount(team);

    public bool HasBotRoom(Faction team) => Mode switch
    {
        MatchMode.Bots => BotCount(team) < MaxTeamSize,
        MatchMode.Normal => Total(team) < TeamSize,
        _ => false,
    };

    public Faction? TeamToJoin()
    {
        if (Mode == MatchMode.Bots) return Count(Faction.Blue) < TeamSize ? Faction.Blue : null;
        int blue = Total(Faction.Blue), red = Total(Faction.Red);
        if (blue >= TeamSize && red >= TeamSize) return null;
        return blue <= red && blue < TeamSize ? Faction.Blue : Faction.Red;
    }
}

public sealed record MatchResult(PlayerId Id, string Name, Faction Team, string Hero, bool Won, int Kills, int Deaths, int BotKills,
    int Points, int LpChange, int LpTotal, int Assists = 0, LpBreakdown? Lp = null);

public sealed record LpBreakdown(double OwnAverage, double OtherAverage, double WinChance, int K, int Raw, int Limited, int Min, int Max, int Applied);

public sealed record ResultRow(int Id, bool Bot, string Name, string Hero, Faction Team, bool Left, int Level, int Kills, int Deaths, int Assists, int Cs,
    long Gold, long ToChampions, long ToObjectives, long ToMinions, long Taken, string[] Items, double Score);
