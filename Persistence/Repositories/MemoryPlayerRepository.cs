using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public sealed class MemoryPlayerRepository : IPlayerRepository
{
    private readonly Dictionary<string, PlayerRecord> store = new(StringComparer.Ordinal);

    public string Description => "pamięć (bez bazy)";

    public Task<PlayerRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default) =>
        Task.FromResult(store.TryGetValue(uid, out PlayerRecord? record) ? Clone(record) : null);

    public Task SaveAsync(PlayerRecord record, CancellationToken cancellationToken = default)
    {
        store[record.Uid] = Clone(record);
        return Task.CompletedTask;
    }

    private static PlayerRecord Clone(PlayerRecord r) => new()
    {
        Uid = r.Uid, Name = r.Name, Team = r.Team, Kills = r.Kills, Deaths = r.Deaths, PlaySeconds = r.PlaySeconds,
        LastWorld = r.LastWorld, LastX = r.LastX, LastY = r.LastY, LastZ = r.LastZ,
    };
}
