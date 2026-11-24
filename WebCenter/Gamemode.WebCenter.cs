namespace BotArena;

public sealed partial class Gamemode
{
    private readonly WebCenter.ChatLog chatLog = new();
    private readonly WebCenter.PerfMeter perf = new();
    private WebCenter.WebCenterServer? webCenter;
    private WebCenter.WebCenterSnapshot? webSnapshot;

    private void StartWebCenter()
    {
        matches.Finished += match => chatLog.Announce(match.Id, match.Title, $"Gra zakończona: wygrali {(match.Winner == Faction.Blue ? "niebiescy" : "czerwoni")} ({match.Score[Faction.Blue]}:{match.Score[Faction.Red]}).");
        matches.Rejoined += player => { if (matches.Of(player) is { } match) chatLog.Announce(match.Id, match.Title, $"{player.Name} wrócił do gry."); };
        try
        {
            if (WebCenter.WebCenterSettings.Load(server.Log) is not { } settings) return;
            webSnapshot = new WebCenter.WebCenterSnapshot(server, matches, storage, perf, new WebCenter.ProcessStats());
            webCenter = new WebCenter.WebCenterServer(settings, webSnapshot, chatLog, storage, text => server.Post(_ => server.Log(text)));
            if (!webCenter.Start()) { webCenter = null; return; }
            webSnapshot.Collect();
            server.Timers.Every(TimeSpan.FromSeconds(1), () => webSnapshot.Collect());
        }
        catch (Exception error)
        {
            server.Log($"[WEB] Web Center nie wystartowal: {error.GetBaseException().Message}");
            webCenter?.Dispose();
            webCenter = null;
        }
    }
}
