using BotArena.Chat;
using BotArena.Matches;
using BotArena.Vitals;
using GothicOnline.Commands;

namespace BotArena;

public sealed partial class Gamemode : IGamemode
{
    private Server server = null!;
    private AdminService admin = null!;
    private AiDebugFeed debugFeed = null!;
    private VitalsService vitals = null!;
    private readonly Dictionary<PlayerId, int> kills = [];
    private TimeSpan elapsed;
    private TimeSpan nextThink, nextFast;

    public void Start(Server server)
    {
        this.server = server;
        debugFeed = new AiDebugFeed(server);
        admin = new AdminService(server);
        StartPersistence();
        StartLobby();
        StartWebCenter();
        vitals = new VitalsService(server, matches);
        vitals.Start();
        matches.Rejoined += vitals.OnJoin;
        admin.MatchOf = matches.Of;

        server.Events.PlayerJoined.Subscribe(e => Join(e.Player));
        server.Events.PlayerDisconnected.Subscribe(e =>
        {
            matches.OnDisconnect(e.Player, auth.AccountOf(e.Player));
            shop.Forget(e.Player);
            profiles.OnLeave(e.Player);
            auth.OnDisconnect(e.Player);
            Teams.Forget(e.Player.Id);
            AutoAttackGate.Forget(e.Player.Id.Value);
            kills.Remove(e.Player.Id);
            debugFeed.Remove(e.Player.Id);
            admin.OnDisconnect(e.Player.Id);
        });
        server.Events.CharacterRespawned.Subscribe(e =>
        {
            if (e.Character is Player player) matches.OnRespawn(player);
        });
        server.Events.PlayerMessage.Subscribe(e =>
        {
            Match? mine = matches.Of(e.Player);
            chatLog.Add(mine?.Id ?? 0, mine?.Title ?? "lobby", e.Player.Name, e.Message);
            foreach (Player other in server.Players)
                if (other.IsValid && ReferenceEquals(matches.Of(other), mine)) other.SendPlayerMessage(e.Player, ColorRgb.White, e.Message);
        });
        server.Commands.Permissions = (player, permission) => permission == "admin" && admin.IsAdmin(player);
        server.Commands.Register(this);
        server.Network.Register<ArenaDebugModeMessage>();
        server.Network.Register<ArenaMobDebugMessage>();
        server.Network.Register<ArenaDamageReportMessage>();
        server.Network.On<ArenaDamageReportMessage>((player, report) => matches.Of(player)?.Director?.ReportDamage(player, report.Attacker, report.Victim, report.Damage, elapsed));
        server.Network.Register<ArenaAdminStateMessage>();
        server.Network.Register<ArenaAdminPlayersMessage>();
        server.Network.Register<ArenaGhostMessage>();
        server.Network.Register<ArenaFreezeMessage>();
        server.Network.Register<ArenaAdminUiMessage>();
        server.Network.Register<ChatCommandsMessage>();
        server.Network.On<ArenaAdminActionMessage>((player, action) => admin.Handle(player, action, elapsed));
        server.Events.Tick.Subscribe(tick =>
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Update(tick);
            perf.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        });
        server.Events.Initialized.Subscribe(() => server.SetTime(new GameTime(12, 0)));
        if (server.IsHotReload)
        {
            server.SetTime(new GameTime(12, 0));
            foreach (Player player in server.Players) Join(player);
        }
    }

    private void Join(Player player)
    {
        matches.EnterLobby(player);
        auth.OnJoin(player);
        player.SendMessage(ColorRgb.Gold, "Witaj w BotArena! Zagraj z botami, z graczami albo w rankingowej, kup bohaterów w sklepie. /help - komendy.");
        if (ArenaSettings.AiViewOnJoin) debugFeed.Set(player, true);
        admin.OnJoin(player);
        server.Network.Send(player, ChatCommandList.Build(server));
        vitals.OnJoin(player);
    }

    private void Update(TickEvent tick)
    {
        elapsed = tick.Elapsed;
        if (elapsed >= nextFast)
        {
            nextFast = elapsed + LaneSettings.HoldCheck;
            matches.FastUpdate(elapsed);
        }
        if (elapsed < nextThink) return;
        nextThink = elapsed + ArenaSettings.ThinkInterval;

        IReadOnlyList<CharacterState> snapshot = server.Snapshot(server.Players);
        admin.Publish(elapsed, snapshot);
        matches.Update(elapsed, snapshot, admin.IsGhost);
    }

    [Command("team", Description = "twoja drużyna albo zmiana: /team niebiescy | /team czerwoni (swoich nie rani się, miniony i wieże swojej drużyny cię nie atakują) (admin)", Aliases = ["druzyna"], Permission = "admin")]
    private string Team(Player player, string name = "")
    {
        string key = name.Trim().ToLowerInvariant();
        Faction? chosen = key switch
        {
            "niebiescy" or "niebieska" or "niebieski" or "blue" or "n" => Faction.Blue,
            "czerwoni" or "czerwona" or "czerwony" or "red" or "c" => Faction.Red,
            _ => null,
        };
        if (chosen is { } team)
        {
            Teams.Set(player, team);
            return $"Jesteś w drużynie {(team == Faction.Blue ? "niebieskiej" : "czerwonej")}.";
        }
        return $"Twoja drużyna: {(Teams.Of(player) == Faction.Blue ? "niebiescy" : "czerwoni")}. Zmiana: /team niebiescy | /team czerwoni.";
    }

    [Command("heal", Description = "przywraca pełne zdrowie (admin)", Permission = "admin")]
    private void Heal(Player player)
    {
        if (!player.IsDead) player.Health = player.MaxHealth;
    }

    [Command("arena", Description = "teleport na środek areny (admin)", Permission = "admin")]
    private void Arena(Player player)
    {
        if (!player.IsDead) player.Position = ArenaSettings.Center;
    }

    [Command("bots", Description = "stan obozów", Aliases = ["moby"])]
    private string Bots(Player player)
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                var lines = ArenaSettings.Camps.Select(camp =>
        {
            var members = director.Mobs.Where(mob => mob.Camp == camp).ToArray();
            return $"{camp.Name} {members.Count(mob => mob.Npc is not null)}/{members.Length}";
        });
        int summoned = director.Mobs.Count(mob => mob.Summoned);
        int blue = director.Mobs.Count(mob => mob.Lane is not null && !mob.Kind.Immobile && mob.Npc is not null && mob.Kind.Faction == Faction.Blue);
        int red = director.Mobs.Count(mob => mob.Lane is not null && !mob.Kind.Immobile && mob.Npc is not null && mob.Kind.Faction == Faction.Red);
        int turrets = director.Mobs.Count(mob => mob.Kind.Immobile && mob.Npc is not null);
        return $"Moby: {director.Alive}/{director.Total}. " + string.Join(", ", lines) + (summoned > 0 ? $", przywołane {summoned}" : "")
            + $". Miniony: niebiescy {blue}, czerwoni {red}, fal {waves.Waves}, wież {turrets}.";
    }

    [Command("wave", Description = "wypuszcza falę minionów z obu baz od razu (admin)", Aliases = ["fala"], Permission = "admin")]
    private string Wave(Player player)
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                int planned = waves.SpawnNow(elapsed);
        return planned == 0 ? $"Limit minionów drużyny na linii: {LaneSettings.MaxPerTeam}." : $"Fala: {planned} minionów wychodzi z obu baz.";
    }

    [Command("kanon", Description = "przywołuje kanoniera (armatę) z bazy: /kanon [niebiescy|czerwoni], domyślnie twoja drużyna (admin)", Aliases = ["cannon", "armata"], Permission = "admin")]
    private string Cannon(Player player, string team = "")
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                Faction side = team.Trim().ToLowerInvariant() switch
        {
            "" => Teams.Of(player),
            "n" or "niebiescy" or "blue" => Faction.Blue,
            "c" or "czerwoni" or "red" => Faction.Red,
            _ => (Faction)(-1),
        };
        if (side is not (Faction.Blue or Faction.Red)) return "Drużyna: niebiescy albo czerwoni.";
        return waves.SpawnCannon(side, player.Position, elapsed)
            ? $"Kanonier ({(side == Faction.Blue ? "niebiescy" : "czerwoni")}) wychodzi z bazy."
            : $"Limit minionów drużyny na linii: {LaneSettings.MaxPerTeam}.";
    }

    [Command("diag", Description = "diagnostyka minionów z linii: stan, host, pozycja, zegar fali, akcje (admin)", Permission = "admin")]
    private string Diag(Player player)
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                var color = ColorRgb.FromRgb(140, 220, 255);
        int shown = 0;
        foreach (Mob m in director.Mobs.Where(m => m.Lane is not null && !m.Kind.Immobile && m.Npc is not null).Take(8))
        {
            LaneState lane = m.Memory.Get<LaneState>();
            float mine = m.Lane!.ProgressOf(m.Kind.Faction, m.LastPosition, lane.Next);
            float clock = m.Wave is { } wave ? wave.Progress + m.WaveOffset : float.NaN;
            player.SendMessage(color, $"{m.Label}: {m.State}, host {m.Host?.Name ?? "-"}, broń {m.Npc!.WeaponMode}, poz ({m.LastPosition.X:0},{m.LastPosition.Z:0}), ja {mine:0}, zegar {clock:0}, tempo {director.Pace.Speed:0}, next {lane.Next}, akcje {m.QueuedActions}, {m.Tree?.ActiveLeaf}");
            shown++;
        }
        return $"Miniony: {shown} pokazanych, fal {waves.Waves}.";
    }

    [Command("spawn", Description = "przywołuje moby obok ciebie, np. /spawn wilk 3 (admin)", Permission = "admin")]
    private string Spawn(Player player, string kind, int count = 1)
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                if (Bestiary.Find(kind) is not { } found)
            return "Nieznany mob. Dostępne: " + string.Join(", ", Bestiary.All.Select(k => k.Key));
        int created = director.Summon(found, player.Position, count, elapsed);
        return created == 0 ? $"Limit przywołanych mobów: {ArenaSettings.MaxSummoned}." : $"Przywołano: {found.DisplayName} x{created} ({found.Behaviour.Name}).";
    }

    [Command("reset", Description = "stawia wszystkie obozy od nowa (admin)", Permission = "admin")]
    private string Reset(Player player)
    {
        if (matches.Of(player) is not { Director: { } director, Waves: { } waves }) return "Nie jesteś w trwającej grze.";
                director.Reset(elapsed);
        return "Obozy postawione od nowa.";
    }

    [Command("ai", Description = "podgląd AI w 3D: typ, gałąź drzewa, cel, pamięć, trasa, promienie omijania (przełącza)")]
    private string Ai(Player player)
    {
        if (!debugFeed.Toggle(player)) return "Podgląd AI 3D wyłączony. /ai włącza.";
        ShowAiStatus(player);
        return "Podgląd AI 3D włączony: nad mobami typ, gałąź drzewa, akcja i pamięć; linie: cel (czerwona), ślad zgubionego celu (żółta), trasa (błękitna), promienie omijania (zielone/czerwone), okręgi: wzrok, pościg, teren. /ai wyłącza.";
    }

    [Command("pos", Description = "zapisuje twoją pozycję z nazwą do logu serwera, np. /pos oboz wilkow", Aliases = ["poz"])]
    private string Pos(Player player, [Rest] string name = "")
    {
        if (string.IsNullOrWhiteSpace(name)) name = "punkt";
        name = name.Trim();
        Position at = player.Position;
        float angle = player.Angle.Degrees;
        float dx = at.X - ArenaSettings.Center.X, dz = at.Z - ArenaSettings.Center.Z;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string position = string.Format(culture, "{0:0}, {1:0}, {2:0}", at.X, at.Y, at.Z);
        server.Log(string.Format(culture, "[POS] {0} | new Position({1}) | kat {2:0} | od srodka areny {3:0}, {4:0} | {5} | {6}",
            name, position, angle, dx, dz, player.World, player.Name));
        return string.Format(culture, "Zapisano do logu: {0} ({1}, kat {2:0}).", name, position, angle);
    }

    [Command("admin", Description = "logowanie admina hasłem: /admin <hasło> (admini z listy UID są adminami od wejścia)")]
    private string AdminLogin(Player player, string password = "") => admin.Login(player, password);

    [Command("ap", Description = "panel admina (jak F6)", Permission = "admin")]
    private void AdminPanel(Player player) => server.Network.Send(player, new ArenaAdminUiMessage(1));

    [Command("fly", Description = "latanie kamerą (jak F7)", Permission = "admin")]
    private void AdminFly(Player player) => server.Network.Send(player, new ArenaAdminUiMessage(2));

    [Command("gold", Description = "daje złoto w trwającej grze: /gold <ilość> [nick|id] — bez gracza sobie, ujemna ilość zabiera (admin)", Aliases = ["zloto"], Permission = "admin")]
    private string AdminGold(Player player, int amount, string who = "")
    {
        Player? target = who.Length == 0 ? player : admin.Find(who);
        if (target is null) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        if (matches.Of(target) is not { } match || !match.Players.TryGetValue(target.Id, out MatchPlayer? mp)) return $"{target.Name} nie jest w trwającej grze.";
        mp.Gold = Math.Clamp(mp.Gold + amount, 0, 999_999);
        matches.RefreshStats(match, mp);
        server.Log($"[ADMIN] {player.Name}: złoto {amount:+#;-#} dla {target.Name} (teraz {mp.Gold})");
        return $"{target.Name}: złoto {mp.Gold} ({amount:+#;-#}).";
    }

    [Command("siatka", Description = "pokazuje / ukrywa siatkę nawigacji NPC w świecie wokół ciebie: /siatka [promień w komórkach 4-30, domyślnie 14] (admin)", Aliases = ["grid", "navgrid"], Permission = "admin")]
    private string NavGridView(Player player, int radius = 14)
    {
        if (!nav.Ready) return "Brak siatki nawigacji (navgrid.nav w gamemodes/BotArena/nav).";
        return nav.ToggleView(player, radius) ? $"Siatka nawigacji włączona (promień {Math.Clamp(radius, 4, 30)} komórek). Zielona = przechodnia, żółta = przy ścianie, niebieska = ścieżka, brązowa = gąszcz, czerwona = blokada. /siatka wyłącza." : "Siatka nawigacji wyłączona.";
    }

    [Command("kick", Description = "wyrzuca gracza: /kick <nick|id> [powód]", Permission = "admin")]
    private string AdminKick(Player player, string who, [Rest] string reason = "")
    {
        if (admin.Find(who) is not { } target) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        if (target.Id == player.Id) return "Nie możesz wyrzucić samego siebie.";
        admin.Kick(player, target, string.IsNullOrWhiteSpace(reason) ? "wyrzucony przez admina" : reason.Trim());
        return $"Wyrzucono: {target.Name}.";
    }

    [Command("ban", Description = "banuje gracza: /ban <nick|id> [minuty] [powód]", Permission = "admin")]
    private string AdminBan(Player player, string who, int minutes = AdminSettings.BanMinutes, [Rest] string reason = "")
    {
        if (admin.Find(who) is not { } target) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        if (target.Id == player.Id) return "Nie możesz zbanować samego siebie.";
        minutes = Math.Max(1, minutes);
        admin.Ban(player, target, TimeSpan.FromMinutes(minutes), string.IsNullOrWhiteSpace(reason) ? "ban admina" : reason.Trim());
        return $"Zbanowano: {target.Name} na {minutes} min.";
    }

    [Command("zabij", Description = "zabija gracza: /zabij <nick|id>", Aliases = ["kill"], Permission = "admin")]
    private string AdminKill(Player player, string who)
    {
        if (admin.Find(who) is not { } target) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        admin.Kill(player, target);
        return $"Zabito: {target.Name}.";
    }

    [Command("idz", Description = "teleportuje cię do gracza: /idz <nick|id>", Aliases = ["goto"], Permission = "admin")]
    private string AdminGoto(Player player, string who)
    {
        if (admin.Find(who) is not { } target) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        return admin.Goto(player, target) ? $"Jesteś przy: {target.Name}." : "Nie możesz teraz.";
    }

    [Command("przywolaj", Description = "teleportuje gracza do ciebie: /przywolaj <nick|id>", Aliases = ["bring"], Permission = "admin")]
    private string AdminBring(Player player, string who)
    {
        if (admin.Find(who) is not { } target) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        return admin.Bring(player, target) ? $"Przywołano: {target.Name}." : "Nie możesz teraz.";
    }

    [Command("stats", Description = "twoje zabicia")]
    private string Stats(Player player) => $"Pokonane moby: {matches.Of(player)?.Players.GetValueOrDefault(player.Id)?.BotKills ?? kills.GetValueOrDefault(player.Id)}.";

    private void ShowAiStatus(Player player)
    {
        if (matches.Of(player) is not { Director: { } director } match) { player.SendMessage(ColorRgb.Gold, "[AI] nie jesteś w trwającej grze"); return; }
        CharacterState me = server.Snapshot([player])[0];
        var problems = new List<string>();
        if (!me.IsSpawned) problems.Add("nie jesteś zespawnowany");
        if (me.IsDead) problems.Add("nie żyjesz");
        if (me.World != ArenaSettings.World) problems.Add($"inny świat ({me.World})");
        if (me.VirtualWorld != match.World) problems.Add($"świat wirtualny {me.VirtualWorld.Value} (gra {match.World.Value})");
        int hosted = director.Mobs.Count(mob => mob.Npc is not null && mob.Host?.Id == player.Id);
        bool visible = me.IsSpawned && !me.IsDead && me.VirtualWorld == match.World && !admin.IsGhost(player.Id);
        player.SendMessage(visible ? ColorRgb.Green : ColorRgb.FromRgb(255, 120, 120),
            $"[AI] Ty: {(visible ? "widoczny dla AI" : "NIEWIDOCZNY dla AI")}, HP {me.Health}/{me.MaxHealth}, " +
            $"{me.Position.DistanceTo(ArenaSettings.Center).Centimeters / 100:0} m od środka areny, symulujesz {hosted}/{director.Alive} mobów" +
            (problems.Count > 0 ? " — " + string.Join(", ", problems) : "") +
            (hosted == 0 && director.Alive > 0 ? " (bez hosta moby stoją: G2O przydziela je graczom zespawnowanym w pobliżu)" : ""));
        ShowNearestMob(player, director, me.Position);
    }

    private void ShowNearestMob(Player player, MobDirector director, Position at)
    {
        Mob? nearest = director.Mobs.Where(mob => mob.Npc is { IsValid: true })
            .MinBy(mob => mob.LastPosition.DistanceTo(at).Centimeters);
        if (nearest?.Npc is not { } npc) { player.SendMessage(ColorRgb.Gold, "[AI] brak mobów"); return; }
        Player? host = nearest.Host;
        string target = nearest.Target switch { Player p => p.Name, Npc n => n.Name, _ => "-" };
        player.SendMessage(ColorRgb.Gold,
            $"[AI] {nearest.Label} ({nearest.Behaviour.Name}): {nearest.Activity}, cel {target}, {npc.Position.DistanceTo(at).Centimeters:0} cm, " +
            $"HP {npc.Health}/{npc.MaxHealth}, kolejka {npc.Actions.Count}, broń {npc.WeaponMode}, host {host?.Name ?? "-"}");
        var memory = nearest.Memory.Known.Select(known =>
            $"{NameOf(known.Character)} {(known.Visible ? "widzi" : $"{known.Age(nearest.LastThink).TotalSeconds:0} s temu")}" +
            (known.Threat > 0 ? $", zagrożenie {known.Threat:0}" : "") + (known.Grudge ? ", uraza" : ""));
        string remembered = string.Join("; ", memory);
        player.SendMessage(ColorRgb.Gold, $"[AI] komponenty: {nearest.Behaviour.Describe()}; pamięta: {(remembered.Length > 0 ? remembered : "nikogo")}");
    }

    private static string NameOf(Character character) => character switch { Player p => p.Name, Npc n => n.Name, _ => "?" };

    public void Stop()
    {
        webCenter?.Dispose();
        matches.Shutdown();
        profiles.SaveAll();
        server.Log("BotArena stopped; games removed.");
    }
}
