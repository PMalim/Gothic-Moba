using BotArena.Persistence.Repositories;

namespace BotArena.Persistence;

public sealed class Storage(Database? database)
{
    public IVobRepository Vobs { get; private set; } = new MemoryVobRepository();
    public IPlayerRepository Players { get; private set; } = new MemoryPlayerRepository();
    public IProfileRepository Profiles { get; private set; } = new MemoryProfileRepository();
    public IAccountRepository Accounts { get; private set; } = new MemoryAccountRepository();
    public IFeedbackRepository Feedback { get; private set; } = new MemoryFeedbackRepository();
    public bool Persistent { get; private set; }
    public string Description { get; private set; } = "pamięć (bez bazy)";
    public Task Ready { get; private set; } = Task.CompletedTask;

    public Task<string> StartAsync()
    {
        Task<string> task = InitializeAsync();
        Ready = task;
        return task;
    }

    private async Task<string> InitializeAsync()
    {
        if (database is null) return "Storage: brak config/mysql.env — voby i gracze tylko w pamięci.";
        try
        {
            IReadOnlyList<string> applied = await database.InitializeAsync().ConfigureAwait(false);
            Vobs = new VobRepository(database);
            Players = new PlayerRepository(database);
            Profiles = new ProfileRepository(database);
            Accounts = new AccountRepository(database);
            Feedback = new FeedbackRepository(database);
            Persistent = true;
            Description = database.Description;
            return $"Storage: {database.Description} gotowa" + (applied.Count > 0 ? $", migracje: {string.Join(", ", applied)}." : ".");
        }
        catch (Exception error)
        {
            return $"Storage: baza niedostępna ({error.GetBaseException().Message}) — voby i gracze tylko w pamięci do restartu.";
        }
    }
}
