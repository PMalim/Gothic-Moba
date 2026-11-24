using System.Text.Json;
using BotArena.Matches;
using BotArena.Persistence;

namespace BotArena.WebCenter;

public sealed class WebCenterSnapshot(Server server, MatchService matches, Storage storage, PerfMeter perf, ProcessStats stats)
{
    private const int HistoryLength = 300;
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly List<double[]> history = [];
    private ProcessStats.Sample sample;
    private (double Average, double Max, int Ticks) tick;
    private DateTime nextSample;
    private string json = "{}";

    public string Json => Volatile.Read(ref json);

    public void Collect()
    {
        if (DateTime.UtcNow >= nextSample)
        {
            nextSample = DateTime.UtcNow.AddSeconds(2);
            sample = stats.Take();
            tick = perf.Take();
            history.Add([DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Math.Round(sample.WorkingSetMb), Math.Round(sample.HeapMb), sample.CpuPercent, Math.Round(tick.Average, 2), Math.Round(tick.Max, 1)]);
            if (history.Count > HistoryLength) history.RemoveAt(0);
        }

        var players = new List<object>();
        foreach (Player player in server.Players)
        {
            if (!player.IsValid) continue;
            Match? match = matches.Of(player);
            players.Add(new
            {
                id = player.Id.Value, name = player.Name, match = match?.Id ?? 0,
                hero = match?.Players.GetValueOrDefault(player.Id)?.Hero ?? "", hp = player.Health, maxHp = player.MaxHealth, dead = player.IsDead,
            });
        }
        object[] games = [.. matches.All.Select(Describe)];
        int mobs = matches.All.Sum(m => m.Director?.Mobs.Count(mob => mob.Npc is not null) ?? 0);
        var state = new
        {
            time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            server = new
            {
                startedAt = new DateTimeOffset(stats.StartedAt).ToUnixTimeSeconds(),
                workingSetMb = Math.Round(sample.WorkingSetMb), privateMb = Math.Round(sample.PrivateMb), heapMb = Math.Round(sample.HeapMb),
                totalMb = Math.Round(sample.TotalMb), cpu = sample.CpuPercent, threads = sample.Threads,
                gen0 = sample.Gen0, gen1 = sample.Gen1, gen2 = sample.Gen2,
                tickAvgMs = Math.Round(tick.Average, 2), tickMaxMs = Math.Round(tick.Max, 1), ticks = tick.Ticks,
                players = players.Count, matches = games.Length, mobs, storage = storage.Description, persistent = storage.Persistent,
            },
            history,
            players,
            matches = games,
        };
        Volatile.Write(ref json, JsonSerializer.Serialize(state, Options));
    }

    private static int Team(Faction team) => team switch { Faction.Blue => 0, Faction.Red => 1, _ => -1 };

    private object Describe(Match match)
    {
        TimeSpan elapsed = match.State switch
        {
            MatchState.Running => matches.Now - match.StartedAt,
            MatchState.Finished => match.EndedAt - match.StartedAt,
            _ => TimeSpan.Zero,
        };
        var roster = new List<object>();
        var champions = new List<object>();
        foreach (MatchPlayer mp in match.Players.Values)
        {
            Player? player = server.Players.Find(mp.Id) is { IsValid: true } found ? found : null;
            Position at = player?.Position ?? default;
            int hp = player?.Health ?? 0, max = player?.MaxHealth ?? 0;
            roster.Add(new
            {
                id = mp.Id.Value, name = mp.Name, team = Team(mp.Team), hero = mp.Hero, level = mp.Level, kills = mp.Kills, deaths = mp.Deaths, assists = mp.Assists, cs = mp.BotKills,
                gold = mp.Gold, items = mp.Items.ToArray(), hp, maxHp = max, dead = player?.IsDead ?? false, ready = mp.Ready,
            });
            if (player is not null && !player.IsDead) champions.Add(new object[] { (int)at.X, (int)at.Z, Team(mp.Team), max > 0 ? hp * 100 / max : 0, 1 });
        }
        var minions = new List<int[]>();
        var structures = new List<int[]>();
        int alive = 0;
        if (match.Director is { } director)
        {
            foreach (Mob mob in director.Mobs)
            {
                if (mob.Npc is not { IsValid: true } || mob.State == MobState.Dead) continue;
                int team = Team(mob.Kind.Faction);
                Position at = mob.LastPosition;
                int hp = mob.LastMaxHealth > 0 ? mob.LastHealth * 100 / mob.LastMaxHealth : 0;
                alive++;
                if (mob.Kind.Immobile)
                {
                    if (mob.Kind.Structure != StructureKind.Dragon)
                        structures.Add([(int)at.X, (int)at.Z, team, hp, mob.Kind.Structure switch { StructureKind.Inhibitor => 1, StructureKind.Nexus => 2, _ => 0 }]);
                }
                else if (mob.Champion) champions.Add(new object[] { (int)at.X, (int)at.Z, team, hp, 0 });
                else if (mob.Lane is not null) minions.Add([(int)at.X, (int)at.Z, team]);
            }
        }
        return new
        {
            id = match.Id, title = match.Title, mode = match.Mode.ToString().ToLowerInvariant(), state = match.State.ToString().ToLowerInvariant(),
            seconds = (int)elapsed.TotalSeconds, blue = match.Score[Faction.Blue], red = match.Score[Faction.Red],
            host = server.Players.Find(match.Host)?.Name ?? "", away = matches.AwayCount(match), alive,
            players = roster,
            bots = match.Bots.Select(b => new { id = b.Id, team = Team(b.Team), hero = b.Hero, name = b.Name, kills = b.Kills, deaths = b.Deaths, assists = b.Assists }),
            map = new { champions, minions, structures },
            winner = match.Winner?.ToString().ToLowerInvariant() ?? "",
        };
    }
}
