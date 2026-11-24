namespace BotArena.Persistence.Models;

public sealed class PlayerRecord
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public int Team { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public long PlaySeconds { get; set; }
    public string? LastWorld { get; set; }
    public float? LastX { get; set; }
    public float? LastY { get; set; }
    public float? LastZ { get; set; }
}
