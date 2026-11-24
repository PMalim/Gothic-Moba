using BotArena.Heroes;
using BotArena.Lobby;
using BotArena.Matches;
using BotArena.Persistence;
using BotArena.Persistence.Models;
using BotArena.Profiles;

namespace BotArena.Feedback;

public sealed class FeedbackService(Server server, Storage storage, ProfileService profiles, MatchService matches, LobbyService lobby)
{
    public const int Limit = 5;
    public const int MaxTitle = 100;
    public const int MaxMessage = 2000;
    public const int MinMessage = 10;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private static readonly HashSet<string> Kinds = ["bug", "complaint", "idea", "balance", "other"];
    private readonly HashSet<PlayerId> sending = [];

    public void Handle(Player player, string kind, int priority, string title, string message)
    {
        kind = kind.Trim().ToLowerInvariant();
        title = title.Trim();
        message = message.Trim();
        if (!Kinds.Contains(kind)) { Fail(player, "Wybierz rodzaj zgłoszenia."); return; }
        if (title.Length == 0) { Fail(player, "Podaj krótki tytuł."); return; }
        if (message.Length < MinMessage) { Fail(player, $"Opisz to dokładniej (co najmniej {MinMessage} znaków)."); return; }
        if (title.Length > MaxTitle) title = title[..MaxTitle];
        if (message.Length > MaxMessage) message = message[..MaxMessage];
        priority = Math.Clamp(priority, 1, 3);
        if (!sending.Add(player.Id)) { Fail(player, "Poprzednie zgłoszenie jeszcze się zapisuje."); return; }

        ProfileRecord? profile = profiles.Of(player);
        string account = profile?.Uid ?? player.Uid;
        string name = profile?.Name ?? player.Name;
        var record = new FeedbackRecord { Account = account, PlayerName = name, Kind = kind, Priority = priority, Title = title, Message = message, Context = ContextOf(player) };
        PlayerId id = player.Id;
        server.Run(async () =>
        {
            try
            {
                await storage.Ready;
                if (await storage.Feedback.CountRecentAsync(account, Window) >= Limit)
                {
                    Reply(id, false, 0, $"Limit: {Limit} zgłoszeń na godzinę. Spróbuj trochę później.");
                    return;
                }
                long number = await storage.Feedback.AddAsync(record);
                server.Log($"[FEEDBACK] #{number} {kind} od {name}: {title}");
                Reply(id, true, number, "");
            }
            catch (Exception problem)
            {
                server.Log($"[FEEDBACK] zapis nieudany: {problem.GetBaseException().Message}");
                Reply(id, false, 0, "Nie udało się zapisać zgłoszenia. Spróbuj ponownie za chwilę.");
            }
        });
    }

    private string ContextOf(Player player)
    {
        if (matches.Of(player) is not { } match) return "lobby";
        string hero = match.Players.TryGetValue(player.Id, out MatchPlayer? mp) ? HeroCatalog.Find(mp.Hero)?.Name ?? mp.Hero : "";
        return hero.Length == 0 ? $"gra #{match.Id}" : $"gra #{match.Id}, {hero}";
    }

    private void Fail(Player player, string error) => lobby.Push(player, "feedback.result", new { ok = false, id = 0L, error });

    private void Reply(PlayerId id, bool ok, long number, string error)
    {
        sending.Remove(id);
        if (server.Players.Find(id) is { IsValid: true } player) lobby.Push(player, "feedback.result", new { ok, id = number, error });
    }
}
