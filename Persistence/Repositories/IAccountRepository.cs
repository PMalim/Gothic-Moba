using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public interface IAccountRepository
{
    Task<AccountRecord?> FindAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> CreateAsync(AccountRecord record, CancellationToken cancellationToken = default);
    Task TouchLoginAsync(string username, string uid, CancellationToken cancellationToken = default);
}
