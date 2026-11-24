namespace BotArena.WebCenter;

public sealed record ChatEntry(long Id, DateTime At, int MatchId, string Title, string Player, string Text, bool System);

public sealed class ChatLog
{
    public const int Capacity = 3000, MaxText = 300;
    private readonly Queue<ChatEntry> entries = new();
    private long nextId = 1;

    public void Add(int matchId, string title, string player, string text) => Push(matchId, title, player, text, system: false);
    public void Announce(int matchId, string title, string text) => Push(matchId, title, "", text, system: true);

    private void Push(int matchId, string title, string player, string text, bool system)
    {
        if (text.Length > MaxText) text = text[..MaxText] + "…";
        lock (entries)
        {
            entries.Enqueue(new ChatEntry(nextId++, DateTime.Now, matchId, title, player, text, system));
            while (entries.Count > Capacity) entries.Dequeue();
        }
    }

    public IReadOnlyList<ChatEntry> Since(long afterId, int? matchId, int limit)
    {
        lock (entries)
        {
            ChatEntry[] found = [.. entries.Where(e => e.Id > afterId && (matchId is null || e.MatchId == matchId))];
            return found.Length <= limit ? found : found[^limit..];
        }
    }

    public long LastId { get { lock (entries) return nextId - 1; } }
}
