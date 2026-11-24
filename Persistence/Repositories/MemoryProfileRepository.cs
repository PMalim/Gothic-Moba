using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public sealed class MemoryProfileRepository : IProfileRepository
{
    private readonly Dictionary<string, ProfileRecord> records = [];

    public Task<ProfileRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default)
    {
        lock (records) return Task.FromResult(records.TryGetValue(uid, out ProfileRecord? record) ? Copy(record) : null);
    }

    public Task SaveAsync(ProfileRecord record, CancellationToken cancellationToken = default)
    {
        lock (records) records[record.Uid] = Copy(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProfileRecord>> TopAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (records) return Task.FromResult<IReadOnlyList<ProfileRecord>>([.. records.Values.OrderByDescending(r => r.Lp).Take(limit).Select(Copy)]);
    }

    private static ProfileRecord Copy(ProfileRecord r) => new()
    {
        Uid = r.Uid, Name = r.Name, Points = r.Points, Lp = r.Lp, Wins = r.Wins, Losses = r.Losses, RankedGames = r.RankedGames,
        SelectedHero = r.SelectedHero, OwnedHeroes = r.OwnedHeroes,
    };
}
