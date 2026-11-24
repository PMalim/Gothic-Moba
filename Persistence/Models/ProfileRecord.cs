namespace BotArena.Persistence.Models;

public sealed class ProfileRecord
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public int Lp { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int RankedGames { get; set; }
    public string SelectedHero { get; set; } = "";
    public string OwnedHeroes { get; set; } = "";
}
