using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public sealed class MemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<string, AccountRecord> records = [];

    public Task<AccountRecord?> FindAsync(string username, CancellationToken cancellationToken = default)
    {
        lock (records) return Task.FromResult(records.GetValueOrDefault(username));
    }

    public Task<bool> CreateAsync(AccountRecord record, CancellationToken cancellationToken = default)
    {
        lock (records) return Task.FromResult(records.TryAdd(record.Username, record));
    }

    public Task TouchLoginAsync(string username, string uid, CancellationToken cancellationToken = default)
    {
        lock (records) if (records.TryGetValue(username, out AccountRecord? record)) record.LastUid = uid;
        return Task.CompletedTask;
    }
}
