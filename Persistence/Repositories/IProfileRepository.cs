using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public interface IProfileRepository
{
    Task<ProfileRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default);
    Task SaveAsync(ProfileRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfileRecord>> TopAsync(int limit, CancellationToken cancellationToken = default);
}
