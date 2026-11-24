using BotArena.Persistence.Models;

namespace BotArena.Persistence.Repositories;

public sealed class MemoryVobRepository : IVobRepository
{
    private readonly Dictionary<int, PlacedVob> store = [];
    private int nextId;

    public string Description => "pamięć (bez bazy — voby znikną po restarcie)";

    public Task<IReadOnlyList<PlacedVob>> ListAsync(string world, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlacedVob>>(store.Values.Where(vob => vob.World == world).OrderBy(vob => vob.Id).Select(vob => vob.Copy()).ToList());

    public Task<int> InsertAsync(PlacedVob vob, CancellationToken cancellationToken = default)
    {
        int id = ++nextId;
        PlacedVob copy = vob.Copy();
        copy.Id = id;
        store[id] = copy;
        return Task.FromResult(id);
    }

    public Task<bool> UpdateAsync(PlacedVob vob, CancellationToken cancellationToken = default)
    {
        if (!store.ContainsKey(vob.Id)) return Task.FromResult(false);
        store[vob.Id] = vob.Copy();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(store.Remove(id));
}
