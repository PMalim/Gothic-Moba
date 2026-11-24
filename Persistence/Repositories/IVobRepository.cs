using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public interface IVobRepository
{
    string Description { get; }
    Task<IReadOnlyList<PlacedVob>> ListAsync(string world, CancellationToken cancellationToken = default);
    Task<int> InsertAsync(PlacedVob vob, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PlacedVob vob, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
