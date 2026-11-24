using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public interface IPlayerRepository
{
    string Description { get; }
    Task<PlayerRecord?> LoadAsync(string uid, CancellationToken cancellationToken = default);
    Task SaveAsync(PlayerRecord record, CancellationToken cancellationToken = default);
}
