using System.Text.RegularExpressions;
using BotArena.Lobby;
using BotArena.Matches;
using BotArena.Persistence;
using BotArena.Persistence.Models;
using BotArena.Profiles;

namespace BotArena.Auth;

public sealed partial class AuthService(Server server, Storage storage, ProfileService profiles, MatchService matches, LobbyService lobby)
{
    public const int MaxFailures = 5;
    private static readonly TimeSpan LockTime = TimeSpan.FromSeconds(30);
    [GeneratedRegex("^[A-Za-z0-9_-]{3,20}$")] private static partial Regex NameRule();

    private readonly Dictionary<PlayerId, string> accounts = [];
    private readonly HashSet<string> online = [];
    private readonly HashSet<PlayerId> busy = [];
    private readonly Dictionary<PlayerId, (int Fails, TimeSpan LockedUntil)> failures = [];

    public bool IsLoggedIn(Player player) => accounts.ContainsKey(player.Id);

    public string? AccountOf(Player player) => accounts.GetValueOrDefault(player.Id);

    public void OnJoin(Player player) => PushState(player);

    public void OnDisconnect(Player player)
    {
        if (accounts.Remove(player.Id, out string? key)) online.Remove(key);
        busy.Remove(player.Id);
        failures.Remove(player.Id);
    }

    public void PushState(Player player, string? error = null) =>
        lobby.Push(player, "auth", new { status = IsLoggedIn(player) ? "ok" : "login", name = profiles.Of(player)?.Name ?? "", error = error ?? "" });

    public void Handle(Player player, string action, string username, string password)
    {
        switch (action)
        {
            case "auth.login": Authenticate(player, username, password, register: false); break;
            case "auth.register": Authenticate(player, username, password, register: true); break;
            case "auth.logout": Logout(player); break;
        }
    }

    private void Authenticate(Player player, string rawName, string password, bool register)
    {
        if (IsLoggedIn(player)) { PushState(player); return; }
        if (busy.Contains(player.Id)) return;
        string display = rawName.Trim();
        if (!NameRule().IsMatch(display)) { Fail(player, "Nazwa: 3-20 znaków, litery bez polskich znaków, cyfry, _ i -.", count: false); return; }
        if (password.Length < 6 || password.Length > 64) { Fail(player, "Hasło musi mieć od 6 do 64 znaków.", count: false); return; }
        if (failures.TryGetValue(player.Id, out var known) && server.Clock.Elapsed < known.LockedUntil)
        {
            Fail(player, $"Za dużo błędnych prób. Poczekaj {(int)Math.Ceiling((known.LockedUntil - server.Clock.Elapsed).TotalSeconds)} s.", count: false);
            return;
        }
        busy.Add(player.Id);
        string key = display.ToLowerInvariant(), uid = player.Uid;
        server.Run(() => AuthenticateAsync(player, key, display, password, uid, register));
    }

    private async Task AuthenticateAsync(Player player, string key, string display, string password, string uid, bool register)
    {
        string? error = null;
        AccountRecord? account = null;
        try
        {
            await storage.Ready;
            if (register)
            {
                (byte[] hash, byte[] salt) = await Task.Run(() => PasswordHasher.Hash(password));
                var record = new AccountRecord { Username = key, DisplayName = display, PassHash = hash, Salt = salt, Iterations = PasswordHasher.Iterations, LastUid = uid };
                if (await storage.Accounts.CreateAsync(record)) account = record;
                else error = "Ta nazwa jest już zajęta.";
            }
            else
            {
                AccountRecord? found = await storage.Accounts.FindAsync(key);
                bool ok = found is not null && await Task.Run(() => PasswordHasher.Verify(password, found.PassHash, found.Salt, found.Iterations));
                if (ok) account = found; else error = "Zła nazwa albo hasło.";
            }
        }
        catch (Exception problem)
        {
            server.Log($"[AUTH] {display}: {problem.GetBaseException().Message}");
            error = "Błąd serwera, spróbuj za chwilę.";
        }
        busy.Remove(player.Id);
        if (!player.IsValid) return;
        if (account is null) { Fail(player, error ?? "Nie udało się.", count: !register); return; }
        if (online.Contains(key)) { Fail(player, "To konto jest już zalogowane.", count: false); return; }

        failures.Remove(player.Id);
        accounts[player.Id] = key;
        online.Add(key);
        string shown = account.DisplayName;
        server.Log($"[AUTH] {shown} {(register ? "zarejestrowany" : "zalogowany")} (uid {uid})");
        server.Run(async () => { try { await storage.Accounts.TouchLoginAsync(key, uid); } catch (Exception problem) { server.Log($"[AUTH] zapis logowania: {problem.GetBaseException().Message}"); } });
        try { player.Name = shown; } catch (Exception problem) { server.Log($"[AUTH] nazwa gracza: {problem.Message}"); }
        profiles.Login(player, "acct:" + key, shown, uid, loaded: p =>
        {
            if (!accounts.ContainsKey(p.Id)) return;
            PushState(p);
            matches.TryRejoin(p, key);
            lobby.PushAll(p);
        });
    }

    private void Fail(Player player, string message, bool count)
    {
        if (count)
        {
            (int fails, TimeSpan until) = failures.GetValueOrDefault(player.Id);
            fails++;
            if (fails >= MaxFailures) { until = server.Clock.Elapsed + LockTime; fails = 0; }
            failures[player.Id] = (fails, until);
        }
        PushState(player, message);
    }

    public void Logout(Player player)
    {
        if (!accounts.Remove(player.Id, out string? key)) return;
        online.Remove(key);
        matches.QueueLeave(player);
        matches.Leave(player);
        profiles.OnLeave(player);
        lobby.Push(player, "room", null);
        PushState(player);
    }
}
