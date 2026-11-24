namespace BotArena;

public static class AdminActions
{
    public const int Kick = 1, Ban = 2, Kill = 3, Heal = 4, Respawn = 5, Goto = 6, Bring = 7, Teleport = 8, Freeze = 9,
        Unfreeze = 10, GhostOn = 11, GhostOff = 12, KillMobs = 13, Wave = 14, Reset = 15;
}

public sealed class AdminService(Server server)
{
    public Func<Player, BotArena.Matches.Match?> MatchOf { get; set; } = _ => null;
    public Func<int> MinionCount { get; set; } = () => 0;

    private static readonly TimeSpan PublishEvery = TimeSpan.FromSeconds(2);
    private readonly HashSet<PlayerId> admins = [];
    private readonly HashSet<PlayerId> ghosts = [];
    private TimeSpan nextPublish;

    public bool IsAdmin(Player player) => admins.Contains(player.Id);
    public bool IsGhost(PlayerId id) => ghosts.Contains(id);

    public void OnJoin(Player player)
    {
        foreach (PlayerId ghost in ghosts) server.Network.Send(player, new ArenaGhostMessage(ghost.Value, true));
        if (AdminSettings.Uids.Contains(player.Uid, StringComparer.Ordinal)) Grant(player, "UID");
    }

    public void OnDisconnect(PlayerId id)
    {
        admins.Remove(id);
        if (ghosts.Remove(id) && server.Players.Count > 0) server.Network.Broadcast(new ArenaGhostMessage(id.Value, false));
    }

    public string Login(Player player, string password)
    {
        if (IsAdmin(player)) return "Jesteś już adminem. Panel: F6, latanie: F7.";
        if (AdminSettings.Password.Length == 0 || !string.Equals(password, AdminSettings.Password, StringComparison.Ordinal))
        {
            server.Log($"[ADMIN] nieudane logowanie: {player.Name}");
            return "Nieprawidłowe hasło.";
        }
        Grant(player, "hasło");
        return "Jesteś adminem.";
    }

    public event Action<Player>? AdminGranted;

    private void Grant(Player player, string how)
    {
        admins.Add(player.Id);
        server.Log($"[ADMIN] {player.Name} jest adminem ({how}).");
        server.Network.Send(player, new ArenaAdminStateMessage(true));
        AdminGranted?.Invoke(player);
        player.SendMessage(ColorRgb.Gold, "Admin: panel F6, latanie kamerą F7. /ap /fly /kick /ban /zabij /idz /przywolaj.");
    }

    public Player? Find(string key)
    {
        key = key.Trim();
        if (int.TryParse(key, out int id)) return server.Players.Find(new PlayerId(id));
        Player[] matches = [.. server.Players.Where(p => p.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase))];
        return matches.Length == 1 ? matches[0] : null;
    }

    public void Kick(Player admin, Player target, string reason)
    {
        server.Log($"[ADMIN] {admin.Name}: kick {target.Name} ({reason})");
        target.Kick(reason);
    }

    public void Ban(Player admin, Player target, TimeSpan duration, string reason)
    {
        server.Log($"[ADMIN] {admin.Name}: ban {target.Name} na {duration.TotalMinutes:0} min ({reason})");
        target.Ban(duration, reason);
    }

    public void Kill(Player admin, Player target)
    {
        server.Log($"[ADMIN] {admin.Name}: zabij {target.Name}");
        if (!target.IsDead) target.Health = 0;
    }

    public bool Goto(Player admin, Player target)
    {
        if (admin.IsDead) return false;
        server.Log($"[ADMIN] {admin.Name}: idz do {target.Name}");
        admin.Position = Beside(target.Position);
        return true;
    }

    public bool Bring(Player admin, Player target)
    {
        if (admin.IsDead || target.IsDead) return false;
        server.Log($"[ADMIN] {admin.Name}: przywołaj {target.Name}");
        target.Position = Beside(admin.Position);
        return true;
    }

    private static Position Beside(Position at) => new(at.X + 150, at.Y + 30, at.Z);

    public void Handle(Player admin, ArenaAdminActionMessage message, TimeSpan now)
    {
        if (!IsAdmin(admin))
        {
            server.Log($"[ADMIN] odrzucono akcję {message.Action} od nie-admina {admin.Name}");
            return;
        }
        Player? target = message.Target >= 0 ? server.Players.Find(new PlayerId(message.Target)) : null;
        bool needsTarget = message.Action is AdminActions.Kick or AdminActions.Ban or AdminActions.Kill or AdminActions.Heal
            or AdminActions.Respawn or AdminActions.Goto or AdminActions.Bring or AdminActions.Freeze or AdminActions.Unfreeze;
        if (needsTarget && target is null)
        {
            admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie ma takiego gracza.");
            return;
        }
        if (target is not null && target.Id == admin.Id && message.Action is AdminActions.Kick or AdminActions.Ban)
        {
            admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie możesz wyrzucić samego siebie.");
            return;
        }

        switch (message.Action)
        {
            case AdminActions.Kick: Kick(admin, target!, "wyrzucony przez admina"); break;
            case AdminActions.Ban: Ban(admin, target!, TimeSpan.FromMinutes(AdminSettings.BanMinutes), "ban admina"); break;
            case AdminActions.Kill: Kill(admin, target!); break;
            case AdminActions.Heal:
                if (!target!.IsDead) target.Health = target.MaxHealth;
                server.Log($"[ADMIN] {admin.Name}: ulecz {target.Name}");
                break;
            case AdminActions.Respawn:
                server.Log($"[ADMIN] {admin.Name}: odrodź {target!.Name}");
                if (target.IsDead) target.Respawn(); else target.Health = target.MaxHealth;
                break;
            case AdminActions.Goto: if (!Goto(admin, target!)) admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie możesz teraz."); break;
            case AdminActions.Bring: if (!Bring(admin, target!)) admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie możesz teraz."); break;
            case AdminActions.Teleport:
                if (!admin.IsDead) admin.Position = new Position(message.X, message.Y, message.Z);
                break;
            case AdminActions.Freeze:
            case AdminActions.Unfreeze:
                server.Network.Send(target!, new ArenaFreezeMessage(message.Action == AdminActions.Freeze));
                server.Log($"[ADMIN] {admin.Name}: {(message.Action == AdminActions.Freeze ? "zamroź" : "odmroź")} {target!.Name}");
                break;
            case AdminActions.GhostOn: SetGhost(admin, true); break;
            case AdminActions.GhostOff: SetGhost(admin, false); break;
            case AdminActions.KillMobs:
                if (MatchOf(admin) is not { Director: { } director, Waves: { } waves } match) { admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie jesteś w trwającej grze."); break; }
                int killed = director.KillMobs();
                server.Log($"[ADMIN] {admin.Name}: zabij moby ({killed})");
                admin.SendMessage(ColorRgb.Green, $"Zabito mobów: {killed}.");
                break;
            case AdminActions.Wave:
                if (MatchOf(admin) is not { Director: { }, Waves: { } wave }) { admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie jesteś w trwającej grze."); break; }
                int planned = wave.SpawnNow(now);
                server.Log($"[ADMIN] {admin.Name}: fala ({planned})");
                admin.SendMessage(ColorRgb.Green, planned == 0 ? "Fala niedostępna (limit minionów albo tryb wyłączony)." : $"Fala: {planned} minionów.");
                break;
            case AdminActions.Reset:
                if (MatchOf(admin) is not { Director: { } resetDirector }) { admin.SendMessage(ColorRgb.FromRgb(255, 120, 120), "Nie jesteś w trwającej grze."); break; }
                resetDirector.Reset(now);
                server.Log($"[ADMIN] {admin.Name}: reset areny");
                admin.SendMessage(ColorRgb.Green, "Arena postawiona od nowa.");
                break;
            default:
                server.Log($"[ADMIN] nieznana akcja {message.Action} od {admin.Name}");
                break;
        }
    }

    public void SetHidden(Player player, bool on)
    {
        bool changed = on ? ghosts.Add(player.Id) : ghosts.Remove(player.Id);
        if (changed && server.Players.Count > 0) server.Network.Broadcast(new ArenaGhostMessage(player.Id.Value, on));
    }

    private void SetGhost(Player admin, bool on)
    {
        bool changed = on ? ghosts.Add(admin.Id) : ghosts.Remove(admin.Id);
        if (!changed) return;
        server.Log($"[ADMIN] {admin.Name}: duch {(on ? "włączony" : "wyłączony")}");
        server.Network.Broadcast(new ArenaGhostMessage(admin.Id.Value, on));
    }

    public void Publish(TimeSpan now, IReadOnlyList<CharacterState> states)
    {
        if (admins.Count == 0 || now < nextPublish) return;
        nextPublish = now + PublishEvery;
        var rows = new List<(Player Player, CharacterState State)>();
        foreach (CharacterState state in states)
            if (state.Character is Player { IsValid: true } player) rows.Add((player, state));
        if (rows.Count == 0) return;

        var message = new ArenaAdminPlayersMessage(
            [.. rows.Select(r => r.Player.Id.Value)],
            [.. rows.Select(r => r.Player.Name)],
            [.. rows.Select(r => r.State.Health)],
            [.. rows.Select(r => r.State.MaxHealth)],
            [.. rows.Select(r => (int)r.Player.Ping.TotalMilliseconds)],
            [.. rows.Select(r => (r.State.IsDead ? 1 : 0) | (ghosts.Contains(r.Player.Id) ? 2 : 0) | (admins.Contains(r.Player.Id) ? 4 : 0))],
            MinionCount());
        foreach (Player player in server.Players)
            if (admins.Contains(player.Id)) server.Network.Send(player, message);
    }
}
