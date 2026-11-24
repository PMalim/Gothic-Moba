namespace BotArena.Persistence.Models;

public sealed class AccountRecord
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public byte[] PassHash { get; set; } = [];
    public byte[] Salt { get; set; } = [];
    public int Iterations { get; set; }
    public string LastUid { get; set; } = "";
}
