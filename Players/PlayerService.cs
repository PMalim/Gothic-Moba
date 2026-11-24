using BotArena.Persistence;
using BotArena.Persistence.Models;

namespace BotArena.Players;

public sealed class PlayerService(Server server, Storage storage, Func<PlayerId, int> sessionKills)
{
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(2);

    private sealed class Session
    {
        public DateTime Since = DateTime.UtcNow;
        public bool Loaded;
        public int KillsBefore, DeathsBefore, Deaths;
        public long PlaySecondsBefore;
    }

    private readonly Dictionary<PlayerId, Session> sessions = [];

    public void Start()
    {
        server.Events.CharacterDied.Subscribe(e =>
        {
            if (e.Victim is Player player && sessions.TryGetValue(player.Id, out Session? session)) session.Deaths++;
        });
        server.Events.Exiting.Subscribe(SaveAllBlocking);
        server.Timers.Every(AutosaveInterval, SaveAll);
    }

    public void OnJoin(Player player)
    {
        sessions[player.Id] = new Session();
        server.Run(() => LoadAsync(player));
    }

    public void OnLeave(Player player)
    {
        PlayerRecord? record = TryCapture(player);
        sessions.Remove(player.Id);
        if (record is not null) Persist(record);
    }

    private async Task LoadAsync(Player player)
    {
        PlayerId id = player.Id;
        string uid = player.Uid;
        PlayerRecord? record;
        try
        {
            await storage.Ready;
            record = await storage.Players.LoadAsync(uid);
        }
        catch (Exception error)
        {
            server.Log($"[GRACZE] nie wczytano {player.Name}: {error.GetBaseException().Message}");
            return;
        }
        if (!player.IsValid || !sessions.TryGetValue(id, out Session? session)) return;
        if (record is not null)
        {
            session.KillsBefore = record.Kills;
            session.DeathsBefore = record.Deaths;
            session.PlaySecondsBefore = record.PlaySeconds;
            player.SendMessage(ColorRgb.Gold, $"Witaj ponownie, {player.Name}! Zabici: {record.Kills}, śmierci: {record.Deaths}.");
        }
        session.Loaded = true;
    }

    private PlayerRecord? TryCapture(Player player)
    {
        if (!sessions.TryGetValue(player.Id, out Session? session) || !session.Loaded || !player.IsValid) return null;
        try
        {
            Position at = player.Position;
            return new PlayerRecord
            {
                Uid = player.Uid, Name = player.Name, Team = Teams.Of(player) == Faction.Red ? 1 : 0,
                Kills = session.KillsBefore + sessionKills(player.Id), Deaths = session.DeathsBefore + session.Deaths,
                PlaySeconds = session.PlaySecondsBefore + (long)(DateTime.UtcNow - session.Since).TotalSeconds,
                LastWorld = player.World.ToString(), LastX = at.X, LastY = at.Y, LastZ = at.Z,
            };
        }
        catch (Exception error)
        {
            server.Log($"[GRACZE] nie odczytano stanu {player.Name}: {error.Message}");
            return null;
        }
    }

    private void Persist(PlayerRecord record) => server.Run(async () =>
    {
        try { await storage.Players.SaveAsync(record); }
        catch (Exception error) { server.Log($"[GRACZE] zapis {record.Name} nieudany: {error.GetBaseException().Message}"); }
    });

    private void SaveAll()
    {
        foreach (Player player in server.Players)
            if (TryCapture(player) is { } record) Persist(record);
    }

    private void SaveAllBlocking()
    {
        PlayerRecord[] records = [.. server.Players.Select(TryCapture).OfType<PlayerRecord>()];
        sessions.Clear();
        if (records.Length == 0) return;
        try
        {
            Task.WhenAll(records.Select(record => storage.Players.SaveAsync(record))).Wait(TimeSpan.FromSeconds(10));
            server.Log($"[GRACZE] zapisano {records.Length} graczy.");
        }
        catch (Exception error) { server.Log($"[GRACZE] zapis przy zatrzymaniu nieudany: {error.GetBaseException().Message}"); }
    }
}
