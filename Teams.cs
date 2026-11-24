namespace BotArena;

public static class Teams
{
    public static readonly Faction Default = Faction.Blue;
    private static readonly Dictionary<PlayerId, Faction> players = [];

    public static Faction Of(Player player) => players.TryGetValue(player.Id, out Faction team) ? team : Default;

    public static Func<Character, bool> Untargetable { get; set; } = _ => false;

    public static void Set(Player player, Faction team) => players[player.Id] = team;

    public static void Forget(PlayerId id) => players.Remove(id);

    public static bool IsEnemy(Faction faction, Character who) =>
        faction is not (Faction.Blue or Faction.Red) || who is not Player player || Factions.Hostile(faction, Of(player));
}
