using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public sealed class MemoryFeedbackRepository : IFeedbackRepository
{
    private readonly List<FeedbackRecord> records = [];

    public Task<long> AddAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
    {
        lock (records)
        {
            record.Id = records.Count + 1;
            records.Add(record);
            return Task.FromResult(record.Id);
        }
    }

    public Task<IReadOnlyList<FeedbackRecord>> ListAsync(int limit, string? status = null, CancellationToken cancellationToken = default)
    {
        lock (records)
            return Task.FromResult<IReadOnlyList<FeedbackRecord>>([.. records.Where(r => status is null || r.Status == status).OrderByDescending(r => r.Id).Take(limit)]);
    }

    public Task<bool> SetStatusAsync(long id, string status, CancellationToken cancellationToken = default)
    {
        lock (records)
        {
            FeedbackRecord? found = records.FirstOrDefault(r => r.Id == id);
            if (found is not null) found.Status = status;
            return Task.FromResult(found is not null);
        }
    }

    public Task<int> CountRecentAsync(string account, TimeSpan window, CancellationToken cancellationToken = default)
    {
        DateTime since = DateTime.Now - window;
        lock (records) return Task.FromResult(records.Count(r => r.Account == account && r.CreatedAt >= since));
    }
}
