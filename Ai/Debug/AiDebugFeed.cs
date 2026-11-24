using System.Text;

namespace BotArena.Ai.Debug;

public sealed class AiDebugFeed(Server server)
{
    public const int KnownVisible = 1, KnownThreat = 2, KnownTarget = 4, KnownUnreachable = 8, KnownGrudge = 16, KnownQuarry = 32;
    private static readonly Distance Radius = new(5000);
    private readonly HashSet<PlayerId> viewers = [];

    public bool IsWatching(Player player) => viewers.Contains(player.Id);

    public bool Toggle(Player player) => Set(player, !viewers.Contains(player.Id));

    public bool Set(Player player, bool enabled)
    {
        if (enabled) viewers.Add(player.Id); else viewers.Remove(player.Id);
        server.Network.Send(player, new ArenaDebugModeMessage(enabled));
        return enabled;
    }

    public void Remove(PlayerId player) => viewers.Remove(player);

    public void Publish(IReadOnlyList<Mob> mobs, IReadOnlyList<Combatant> players)
    {
        if (viewers.Count == 0) return;
        var described = new Dictionary<Mob, ArenaMobDebugMessage>();
        foreach (PlayerId id in viewers.ToArray())
        {
            if (server.Players.Find(id) is not { } viewer) { viewers.Remove(id); continue; }
            Position? at = null;
            foreach (Combatant player in players) if (ReferenceEquals(player.Character, viewer)) at = player.Position;
            if (at is null) continue;
            foreach (Mob mob in mobs)
            {
                if (mob.Npc is not { IsValid: true } npc || mob.State == MobState.Dead) continue;
                if (mob.LastPosition.DistanceTo(at.Value) > Radius) continue;
                try
                {
                    if (!described.TryGetValue(mob, out ArenaMobDebugMessage? message)) described[mob] = message = Describe(mob, npc, mob.LastThink);
                    server.Network.Send(viewer, message);
                }
                catch (ObjectDisposedException) { }
            }
        }
    }

    private static ArenaMobDebugMessage Describe(Mob mob, Npc npc, TimeSpan now)
    {
        Character? target = mob.Target ?? mob.Quarry;
        if (target is { IsValid: false }) target = null;
        string state = mob.State == MobState.Dormant ? "bez hosta" : mob.Tree?.ActiveBranch is { Length: > 0 } branch ? branch : "spokój";
        MobBehaviour behaviour = mob.Behaviour;
        var known = mob.Memory.Known.Where(entry => entry.Character.IsValid).Take(8).ToArray();
        return new ArenaMobDebugMessage(
            npc.Id.Value, Ascii(mob.Label), Ascii(state), Ascii(mob.Tree?.ActivePath ?? ""), Ascii(mob.Activity),
            target is null ? -1 : MobCommands.IdOf(target), Ascii(NameOf(target)),
            mob.Home, mob.Focus ?? default, (int)mob.FocusKind, mob.Kind.SightRange.Centimeters, mob.Kind.AttackRange.Centimeters,
            mob.LastHealth, mob.LastMaxHealth, mob.FlankAngle, mob.QueuedActions, Ascii(mob.Note),
            Ascii(behaviour.Name), Ascii(behaviour.Describe()),
            behaviour.Get<Perception>()?.PursuitRange(mob.Kind).Centimeters ?? 0f,
            behaviour.Get<Territory>()?.Leash.Centimeters ?? 0f,
            [.. known.Select(entry => MobCommands.IdOf(entry.Character))],
            [.. known.Select(entry => entry.LastSeen)],
            [.. known.Select(entry => Flags(mob, entry))],
            [.. known.Select(entry => (int)Math.Clamp(entry.Age(now).TotalMilliseconds, 0, int.MaxValue))]);
    }

    private static int Flags(Mob mob, KnownCharacter entry) =>
        (entry.Visible ? KnownVisible : 0) | (entry.Threat > 0 ? KnownThreat : 0)
        | (ReferenceEquals(entry.Character, mob.Target) ? KnownTarget : 0) | (entry.UnreachableSince is not null ? KnownUnreachable : 0)
        | (entry.Grudge ? KnownGrudge : 0) | (ReferenceEquals(entry.Character, mob.Quarry) ? KnownQuarry : 0);

    private static string NameOf(Character? character)
    {
        if (character is not { IsValid: true }) return "";
        try
        {
            return character switch { Player player => player.Name, Npc npc => npc.Name, _ => "" };
        }
        catch (ObjectDisposedException) { return ""; }
    }

    public static string Ascii(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            result.Append(ch switch
            {
                'ą' => 'a', 'ć' => 'c', 'ę' => 'e', 'ł' => 'l', 'ń' => 'n', 'ó' => 'o', 'ś' => 's', 'ź' => 'z', 'ż' => 'z',
                'Ą' => 'A', 'Ć' => 'C', 'Ę' => 'E', 'Ł' => 'L', 'Ń' => 'N', 'Ó' => 'O', 'Ś' => 'S', 'Ź' => 'Z', 'Ż' => 'Z',
                '›' => '>', '—' or '–' => '-', '„' or '”' or '“' => '"', '×' => 'x',
                < (char)128 => ch,
                _ => '?',
            });
        }
        return result.ToString();
    }
}
