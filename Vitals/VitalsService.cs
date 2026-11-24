using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Vitals;

public sealed class VitalsService(Server server, MatchService matches)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private readonly Dictionary<int, (Row[] Key, ArenaRosterMessage Message)> rosters = [];

    public void Start()
    {
        server.Network.Register<ArenaRosterMessage>();
        server.Network.Register<ArenaVitalsMessage>();
        server.Timers.Every(Interval, Publish);
    }

    public void OnJoin(Player player)
    {
        if (matches.Of(player) is { } match && rosters.TryGetValue(match.Id, out var roster)) server.Network.Send(player, roster.Message);
    }

    private readonly record struct Row(int Id, int Kind, string Name, int Team, int Health, int MaxHealth, Position At, int Locked = 0);

    private static int TeamOf(Faction faction) => faction switch { Faction.Blue => 0, Faction.Red => 1, _ => -1 };

    private List<Row> Collect(Match match)
    {
        var rows = new List<Row>();
        foreach (Mob mob in match.Director!.Mobs)
        {
            if (mob.Npc is not { IsValid: true } npc || mob.Kind.Structure == StructureKind.Dragon) continue;
            int maxHealth = mob.LastMaxHealth > 0 ? mob.LastMaxHealth : mob.Kind.Health;
            int kind = mob.Behaviour.Has<DrakeCombat>() ? 5
                : mob.Behaviour.Has<BaronCombat>() ? 6
                : mob.Kind.Structure switch { StructureKind.Inhibitor => 3, StructureKind.Nexus => 4, _ => mob.Kind.Immobile ? 1 : 0 };
            rows.Add(new Row(npc.Id.Value, kind, mob.Kind.DisplayName, TeamOf(mob.Kind.Faction),
                Math.Max(0, mob.LastHealth > 0 || mob.State != MobState.Dead ? mob.LastHealth : 0), maxHealth, mob.LastPosition,
                mob.Kind.Immobile && match.Director.IsProtected(mob) ? 1 : 0));
        }
        foreach (MatchPlayer mp in match.Players.Values)
        {
            if (server.Players.Find(mp.Id) is not { IsValid: true } player) continue;
            rows.Add(new Row(player.Id.Value, 2, player.Name, TeamOf(mp.Team), Math.Max(0, player.Health), player.MaxHealth, player.Position));
        }
        return rows;
    }

    private static bool SameRoster(Row[] known, List<Row> rows)
    {
        if (known.Length != rows.Count) return false;
        for (int i = 0; i < known.Length; i++)
        {
            Row a = known[i], b = rows[i];
            if (a.Id != b.Id || a.Kind != b.Kind || a.Team != b.Team || a.Locked != b.Locked || !ReferenceEquals(a.Name, b.Name) && a.Name != b.Name) return false;
        }
        return true;
    }

    private void Publish()
    {
        if (server.Players.Count == 0) return;
        foreach (Match match in matches.All)
        {
            if (match.Director is null || match.State is not (MatchState.Running or MatchState.Finished) || match.Players.Count == 0) continue;
            List<Row> rows = Collect(match);
            if (!rosters.TryGetValue(match.Id, out var known) || !SameRoster(known.Key, rows))
            {
                var roster = new ArenaRosterMessage([.. rows.Select(r => r.Id)], [.. rows.Select(r => r.Kind)], [.. rows.Select(r => r.Name)], [.. rows.Select(r => r.Team)], [.. rows.Select(r => r.Locked)]);
                rosters[match.Id] = ([.. rows], roster);
                matches.Send(match, roster);
            }
            matches.Send(match, new ArenaVitalsMessage([.. rows.Select(r => r.Id)], [.. rows.Select(r => r.Health)],
                [.. rows.Select(r => r.MaxHealth)], [.. rows.Select(r => r.At)]), PacketReliability.UnreliableSequenced);
        }
        foreach (int id in rosters.Keys.Where(id => !matches.All.Any(m => m.Id == id)).ToArray()) rosters.Remove(id);
    }
}
