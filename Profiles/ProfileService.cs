using BotArena.Heroes;
using BotArena.Persistence;
using BotArena.Persistence.Models;

namespace BotArena.Profiles;

public sealed class ProfileService(Server server, Storage storage)
{
    private readonly Dictionary<PlayerId, ProfileRecord> profiles = [];
    private readonly HashSet<PlayerId> loading = [];

    public event Action<Player>? Changed;

    public ProfileRecord? Of(Player player) => profiles.GetValueOrDefault(player.Id);
    public bool IsLoaded(Player player) => profiles.ContainsKey(player.Id);

    public void Login(Player player, string key, string name, string legacyUid, Action<Player> loaded)
    {
        loading.Add(player.Id);
        server.Run(() => LoadAsync(player, key, name, legacyUid, loaded));
    }

    public void OnLeave(Player player)
    {
        if (profiles.Remove(player.Id, out ProfileRecord? record)) Persist(record);
        loading.Remove(player.Id);
    }

    private const string ClaimedMark = "~przeniesiony";

    private async Task LoadAsync(Player player, string key, string name, string legacyUid, Action<Player> loaded)
    {
        PlayerId id = player.Id;
        ProfileRecord? record;
        try
        {
            await storage.Ready;
            record = await storage.Profiles.LoadAsync(key);
            if (record is null && legacyUid.Length > 0 && await storage.Profiles.LoadAsync(legacyUid) is { } legacy && legacy.SelectedHero != ClaimedMark)
            {
                record = new ProfileRecord
                {
                    Uid = key, Name = name, Points = legacy.Points, Lp = legacy.Lp, Wins = legacy.Wins, Losses = legacy.Losses,
                    RankedGames = legacy.RankedGames, SelectedHero = legacy.SelectedHero, OwnedHeroes = legacy.OwnedHeroes,
                };
                legacy.SelectedHero = ClaimedMark;
                await storage.Profiles.SaveAsync(legacy);
                server.Log($"[PROFILE] {name}: przejęto stary profil klienta");
            }
        }
        catch (Exception error)
        {
            server.Log($"[PROFILE] nie wczytano profilu {name}: {error.GetBaseException().Message}");
            record = null;
        }
        loading.Remove(id);
        if (!player.IsValid) return;
        record ??= new ProfileRecord { Uid = key, Name = name, Points = ProfileSettings.StartingPoints };
        record.Name = name;
        Normalize(record);
        profiles[id] = record;
        Persist(record);
        Changed?.Invoke(player);
        loaded(player);
    }

    private static void Normalize(ProfileRecord record)
    {
        HashSet<string> owned = Owned(record);
        foreach (HeroDef free in HeroCatalog.Free) owned.Add(free.Key);
        record.OwnedHeroes = string.Join(',', owned);
        if (!owned.Contains(record.SelectedHero)) record.SelectedHero = HeroCatalog.Free.First().Key;
    }

    public static HashSet<string> Owned(ProfileRecord record) =>
        [.. record.OwnedHeroes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public string? Buy(Player player, string heroKey)
    {
        if (Of(player) is not { } profile) return "Profil się wczytuje.";
        if (HeroCatalog.Find(heroKey) is not { } hero) return "Nie ma takiego bohatera.";
        HashSet<string> owned = Owned(profile);
        if (owned.Contains(hero.Key)) return "Już masz tego bohatera.";
        if (profile.Points < hero.Price) return $"Brakuje {hero.Price - profile.Points} punktów.";
        profile.Points -= hero.Price;
        owned.Add(hero.Key);
        profile.OwnedHeroes = string.Join(',', owned);
        Persist(profile);
        Changed?.Invoke(player);
        return null;
    }

    public string? Select(Player player, string heroKey)
    {
        if (Of(player) is not { } profile) return "Profil się wczytuje.";
        if (HeroCatalog.Find(heroKey) is not { } hero || !Owned(profile).Contains(hero.Key)) return "Nie masz tego bohatera.";
        profile.SelectedHero = hero.Key;
        Persist(profile);
        Changed?.Invoke(player);
        return null;
    }

    public (int Points, int LpChange) Award(Player player, bool won, int points, int lpChange, bool ranked)
    {
        if (Of(player) is not { } profile) return (0, 0);
        profile.Points += points;
        if (won) profile.Wins++; else profile.Losses++;
        int applied = 0;
        if (ranked)
        {
            int before = profile.Lp;
            profile.Lp = Math.Max(0, profile.Lp + lpChange);
            applied = profile.Lp - before;
            profile.RankedGames++;
        }
        Persist(profile);
        Changed?.Invoke(player);
        return (points, applied);
    }

    public void PenalizeOffline(string uid, int lpChange)
    {
        foreach ((PlayerId id, ProfileRecord live) in profiles)
        {
            if (live.Uid != uid) continue;
            live.Lp = Math.Max(0, live.Lp + lpChange);
            live.Losses++;
            live.RankedGames++;
            Persist(live);
            if (server.Players.Find(id) is { IsValid: true } online) Changed?.Invoke(online);
            return;
        }
        server.Run(async () =>
        {
            try
            {
                await storage.Ready;
                if (await storage.Profiles.LoadAsync(uid) is not { } record) return;
                record.Lp = Math.Max(0, record.Lp + lpChange);
                record.Losses++;
                record.RankedGames++;
                await storage.Profiles.SaveAsync(record);
            }
            catch (Exception error) { server.Log($"[PROFILE] kara LP dla {uid} nieudana: {error.GetBaseException().Message}"); }
        });
    }

    public int RankOf(Player player) =>
        Of(player) is { } profile ? 1 + profiles.Values.Count(p => p.Lp > profile.Lp) : 0;

    public void Top(int limit, Action<IReadOnlyList<ProfileRecord>> then) =>
        server.Run(async () =>
        {
            IReadOnlyList<ProfileRecord> top;
            try
            {
                await storage.Ready;
                top = await storage.Profiles.TopAsync(limit);
            }
            catch (Exception error)
            {
                server.Log($"[PROFILE] ranking: {error.GetBaseException().Message}");
                top = [];
            }
            var merged = top.ToDictionary(r => r.Uid);
            foreach (ProfileRecord live in profiles.Values) merged[live.Uid] = live;
            then([.. merged.Values.OrderByDescending(r => r.Lp).ThenByDescending(r => r.Wins).Take(limit)]);
        });

    private void Persist(ProfileRecord record)
    {
        ProfileRecord copy = new()
        {
            Uid = record.Uid, Name = record.Name, Points = record.Points, Lp = record.Lp, Wins = record.Wins, Losses = record.Losses,
            RankedGames = record.RankedGames, SelectedHero = record.SelectedHero, OwnedHeroes = record.OwnedHeroes,
        };
        server.Run(async () =>
        {
            try { await storage.Ready; await storage.Profiles.SaveAsync(copy); }
            catch (Exception error) { server.Log($"[PROFILE] zapis {copy.Name} nieudany: {error.GetBaseException().Message}"); }
        });
    }

    public void SaveAll()
    {
        foreach (ProfileRecord record in profiles.Values) Persist(record);
    }
}

public static class ProfileSettings
{
    public const int StartingPoints = 150;
    public const int WinPoints = 120, LossPoints = 40, PerKill = 6;
    public const float BotsMultiplier = 0.5f;
    public const int LpK = 32;
}
