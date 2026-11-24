namespace BotArena.Persistence.Models;

public sealed class FeedbackRecord
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Account { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Kind { get; set; } = "other";
    public int Priority { get; set; } = 2;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Context { get; set; } = "";
    public string Status { get; set; } = "new";
}
