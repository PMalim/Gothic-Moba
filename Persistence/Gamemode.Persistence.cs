using BotArena.Persistence;
using BotArena.Players;
using BotArena.WorldBuilder;
using GothicOnline.Commands;

namespace BotArena;

public sealed partial class Gamemode
{
    private Storage storage = null!;
    private BuilderService builder = null!;
    private PlayerService playerStore = null!;

    private void StartPersistence()
    {
        storage = new Storage(Database.Create());
        server.Run(async () => server.Log(await storage.StartAsync()));

        builder = new BuilderService(server, storage, admin.IsAdmin);
        builder.Start();
        admin.AdminGranted += builder.SendState;

        playerStore = new PlayerService(server, storage, id => kills.GetValueOrDefault(id));
        playerStore.Start();

        server.Events.PlayerJoined.Subscribe(e => { playerStore.OnJoin(e.Player); builder.OnJoin(e.Player); });
        server.Events.PlayerDisconnected.Subscribe(e => playerStore.OnLeave(e.Player));
        if (server.IsHotReload)
            foreach (Player player in server.Players) { playerStore.OnJoin(player); builder.OnJoin(player); }
    }

    [Command("vobs", Description = "world builder: filtr katalogu vobów, np. /vobs barrel (bez frazy czyści filtr) (admin)", Permission = "admin")]
    private string VobsFilter(Player player, [Rest] string text = "")
    {
        builder.SendFilter(player, text.Trim());
        return text.Trim().Length == 0 ? "Filtr katalogu wyczyszczony." : $"Filtr katalogu: {text.Trim()}";
    }

    [Command("vobinfo", Description = "world builder: ile vobów jest w świecie i gdzie są zapisane (admin)", Permission = "admin")]
    private string VobInfo(Player player) => $"World builder: {builder.Count} vobów, magazyn: {builder.StorageDescription}. F8 włącza budowanie.";
}
