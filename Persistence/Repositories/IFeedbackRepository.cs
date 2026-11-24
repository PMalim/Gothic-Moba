using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public interface IFeedbackRepository
{
    Task<long> AddAsync(FeedbackRecord record, CancellationToken cancellationToken = default);
    Task<int> CountRecentAsync(string account, TimeSpan window, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackRecord>> ListAsync(int limit, string? status = null, CancellationToken cancellationToken = default);
    Task<bool> SetStatusAsync(long id, string status, CancellationToken cancellationToken = default);
}
