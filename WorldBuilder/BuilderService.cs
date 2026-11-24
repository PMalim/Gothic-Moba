using BotArena.Persistence;
using BotArena.Persistence.Models;

namespace BotArena.WorldBuilder;

public sealed class BuilderService(Server server, Storage storage, Func<Player, bool> isAdmin)
{
    private readonly Dictionary<int, PlacedVob> vobs = [];
    private readonly Dictionary<PlayerId, (TimeSpan Window, int Count)> limits = [];
    private bool loaded;

    private static string World => ArenaSettings.World.ToString();

    public int Count => vobs.Count;
    public string StorageDescription => storage.Description;

    public void Start()
    {
        server.Network.Register<BuilderStateMessage>();
        server.Network.Register<BuilderVobsMessage>();
        server.Network.Register<BuilderGoneMessage>();
        server.Network.Register<BuilderFilterMessage>();
        server.Network.On<BuilderPlaceMessage>(Place);
        server.Network.On<BuilderEditMessage>(Edit);
        server.Network.On<BuilderDeleteMessage>(Delete);
        server.Run(LoadAsync);
    }

    private async Task LoadAsync()
    {
        try
        {
            await storage.Ready;
            IReadOnlyList<PlacedVob> list = await storage.Vobs.ListAsync(World);
            vobs.Clear();
            foreach (PlacedVob vob in list) vobs[vob.Id] = vob;
            loaded = true;
            server.Log($"[BUILDER] {vobs.Count} vobów wczytanych ({storage.Description}).");
            foreach (Player player in server.Players) SendWorld(player);
        }
        catch (Exception error)
        {
            server.Log($"[BUILDER] nie wczytano vobów: {error.GetBaseException().Message}");
        }
    }

    public void OnJoin(Player player)
    {
        SendState(player);
        if (loaded) SendWorld(player);
    }

    public void SendState(Player player) =>
        server.Network.Send(player, new BuilderStateMessage(isAdmin(player), storage.Description));

    public void SendFilter(Player player, string text) => server.Network.Send(player, new BuilderFilterMessage(text));

    private bool Allowed(Player player)
    {
        if (!isAdmin(player))
        {
            server.Log($"[BUILDER] odrzucono akcję nie-admina {player.Name}");
            return false;
        }
        if (!loaded)
        {
            player.SendMessage(ColorRgb.FromRgb(255, 160, 120), "Builder: świat jeszcze się wczytuje z bazy, poczekaj chwilę.");
            return false;
        }
        TimeSpan now = server.Clock.Elapsed;
        (TimeSpan window, int count) = limits.GetValueOrDefault(player.Id);
        if (now - window > TimeSpan.FromSeconds(1)) (window, count) = (now, 0);
        limits[player.Id] = (window, ++count);
        return count <= BuilderSettings.MaxActionsPerSecond;
    }

    private static bool Sane(Position at, float yaw, float pitch, float roll, float scale) =>
        float.IsFinite(at.X) && float.IsFinite(at.Y) && float.IsFinite(at.Z) && float.IsFinite(yaw) && float.IsFinite(pitch)
        && float.IsFinite(roll) && float.IsFinite(scale)
        && MathF.Abs(at.X) <= BuilderSettings.MaxCoordinate && MathF.Abs(at.Y) <= BuilderSettings.MaxCoordinate && MathF.Abs(at.Z) <= BuilderSettings.MaxCoordinate;

    private static float Degrees(float angle) => angle % 360f;

    private void Place(Player player, BuilderPlaceMessage message)
    {
        if (!Allowed(player)) return;
        if (!BuilderSettings.IsValidVisual(message.Visual) || !Sane(message.At, message.Yaw, message.Pitch, message.Roll, message.Scale))
        {
            player.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Builder: odrzucono — zła nazwa visuala albo wartości.");
            return;
        }
        if (vobs.Count >= BuilderSettings.MaxVobs)
        {
            player.SendMessage(ColorRgb.FromRgb(255, 120, 120), $"Builder: limit {BuilderSettings.MaxVobs} vobów.");
            return;
        }
        var vob = new PlacedVob
        {
            World = World, Visual = message.Visual.ToUpperInvariant(), X = message.At.X, Y = message.At.Y, Z = message.At.Z,
            Yaw = Degrees(message.Yaw), Pitch = Degrees(message.Pitch), Roll = Degrees(message.Roll),
            Scale = Math.Clamp(message.Scale, BuilderSettings.MinScale, BuilderSettings.MaxScale), Collision = message.Collision,
            PlacedBy = player.Name, PlacedUid = player.Uid,
        };
        server.Run(async () =>
        {
            try
            {
                vob.Id = await storage.Vobs.InsertAsync(vob);
            }
            catch (Exception error)
            {
                server.Log($"[BUILDER] zapis voba nieudany: {error.GetBaseException().Message}");
                if (player.IsValid) player.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Builder: zapis w bazie nieudany, vob nie został postawiony.");
                return;
            }
            vobs[vob.Id] = vob;
            Broadcast([vob]);
            server.Log($"[BUILDER] {vob.PlacedBy} postawił #{vob.Id} {vob.Visual} ({vob.X:0},{vob.Y:0},{vob.Z:0}) yaw {vob.Yaw:0}");
        });
    }

    private void Edit(Player player, BuilderEditMessage message)
    {
        if (!Allowed(player)) return;
        if (!vobs.TryGetValue(message.Id, out PlacedVob? current) || !Sane(message.At, message.Yaw, message.Pitch, message.Roll, message.Scale)) return;
        PlacedVob vob = current.Copy();
        vob.X = message.At.X; vob.Y = message.At.Y; vob.Z = message.At.Z;
        vob.Yaw = Degrees(message.Yaw); vob.Pitch = Degrees(message.Pitch); vob.Roll = Degrees(message.Roll);
        vob.Scale = Math.Clamp(message.Scale, BuilderSettings.MinScale, BuilderSettings.MaxScale);
        vob.Collision = message.Collision;
        vobs[vob.Id] = vob;
        Broadcast([vob]);
        server.Run(async () =>
        {
            try { await storage.Vobs.UpdateAsync(vob); }
            catch (Exception error) { server.Log($"[BUILDER] zapis zmiany #{vob.Id} nieudany: {error.GetBaseException().Message}"); }
        });
    }

    private void Delete(Player player, BuilderDeleteMessage message)
    {
        if (!Allowed(player)) return;
        if (!vobs.Remove(message.Id, out PlacedVob? vob)) return;
        server.Network.Broadcast(new BuilderGoneMessage(message.Id));
        server.Log($"[BUILDER] {player.Name} usunął #{vob.Id} {vob.Visual}");
        server.Run(async () =>
        {
            try { await storage.Vobs.DeleteAsync(message.Id); }
            catch (Exception error) { server.Log($"[BUILDER] usunięcie #{message.Id} z bazy nieudane: {error.GetBaseException().Message}"); }
        });
    }

    private static BuilderVobsMessage Pack(IReadOnlyList<PlacedVob> list, bool reset)
    {
        var angles = new float[list.Count * 3];
        for (int i = 0; i < list.Count; i++)
        {
            angles[i * 3] = list[i].Yaw; angles[i * 3 + 1] = list[i].Pitch; angles[i * 3 + 2] = list[i].Roll;
        }
        return new BuilderVobsMessage(reset, [.. list.Select(v => v.Id)], [.. list.Select(v => v.Visual)],
            [.. list.Select(v => new Position(v.X, v.Y, v.Z))], angles, [.. list.Select(v => v.Scale)],
            [.. list.Select(v => v.Collision ? 1 : 0)]);
    }

    private void Broadcast(IReadOnlyList<PlacedVob> list)
    {
        if (server.Players.Count > 0) server.Network.Broadcast(Pack(list, reset: false));
    }

    private void SendWorld(Player player)
    {
        PlacedVob[] all = [.. vobs.Values.OrderBy(v => v.Id)];
        if (all.Length == 0) { server.Network.Send(player, Pack([], reset: true)); return; }
        for (int i = 0; i < all.Length; i += BuilderSettings.SyncBatch)
            server.Network.Send(player, Pack(all[i..Math.Min(i + BuilderSettings.SyncBatch, all.Length)], reset: i == 0));
    }
}
