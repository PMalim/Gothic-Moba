using System.Text.Encodings.Web;
using System.Text.Json;
using BotArena.Auth;
using BotArena.Heroes;
using BotArena.Matches;
using BotArena.Persistence.Models;
using BotArena.Profiles;

namespace BotArena.Lobby;

public sealed class LobbyService(Server server, ProfileService profiles, MatchService matches)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Default,
    };

    private IReadOnlyList<ProfileRecord> ranking = [];
    private readonly Dictionary<PlayerId, (float X, float Z)> facing = [];

    public (float X, float Z) FacingOf(Player player) => facing.GetValueOrDefault(player.Id);

    public AuthService? Auth { get; set; }
    public Func<Player, HeroStats?> StatsOf { get; set; } = _ => null;
    public Func<Player, (int Damage, bool Magic, int CritPercent)?> BasicOf { get; set; } = _ => null;
    public Action<Player, string, string, int>? Shop { get; set; }
    public Action<Player, string, string>? Nav { get; set; }
    public Action<Player, string, int, string, string>? Feedback { get; set; }
    public Func<Player, int, bool>? SkillUp { get; set; }

    public void Start()
    {
        server.Network.Register<UiPushMessage>();
        server.Network.Register<UiActionMessage>();
        server.Network.On<UiActionMessage>(Handle);
        profiles.Changed += player => { PushProfile(player); if (matches.Of(player) is { } m) PushRoom(m); };
        matches.RoomsChanged += PushRoomsToLobby;
        matches.RoomChanged += PushRoom;
        matches.QueueChanged += PushQueue;
        matches.Finished += PushResults;
        matches.ScoreChanged += PushScore;
        matches.StatsChanged += PushMe;
        server.Timers.Every(TimeSpan.FromMilliseconds(500), PushMinimaps);
    }

    private const float MinimapVision = 3500f;

    private void PushMinimaps()
    {
        foreach (Match match in matches.All)
        {
            if (match.State != MatchState.Running || match.Director is not { } director) continue;
            var champions = new List<(float X, float Z, int Team, PlayerId? Who, int Hp, string Hero)>();
            var minions = new List<(float X, float Z, int Team)>();
            var towers = new List<(float X, float Z, int Team, int Hp, int Type)>();
            foreach (MatchPlayer mp in match.Players.Values)
                if (server.Players.Find(mp.Id) is { IsValid: true, IsDead: false } who)
                {
                    Position at = who.Position;
                    champions.Add((at.X, at.Z, TeamIndex(mp.Team), mp.Id, who.MaxHealth > 0 ? who.Health * 100 / who.MaxHealth : 0, mp.Hero));
                }
            foreach (Mob mob in director.Mobs)
            {
                if (mob.Npc is not { IsValid: true } || mob.State == MobState.Dead) continue;
                int team = TeamIndex(mob.Kind.Faction);
                if (team < 0) continue;
                Position at = mob.LastPosition;
                int hp = mob.LastMaxHealth > 0 ? mob.LastHealth * 100 / mob.LastMaxHealth : 0;
                if (mob.Kind.Immobile)
                {
                    if (mob.Kind.Structure != StructureKind.Dragon) towers.Add((at.X, at.Z, team, hp, mob.Kind.Structure switch { StructureKind.Inhibitor => 1, StructureKind.Nexus => 2, _ => 0 }));
                }
                else if (mob.Champion) champions.Add((at.X, at.Z, team, null, hp, match.Bots.FirstOrDefault(b => b.Id == mob.BotId)?.Hero ?? ""));
                else if (mob.Lane is not null || mob.IsHelper) minions.Add((at.X, at.Z, team));
            }
            var seeing = new[] { new List<(float X, float Z)>(), new List<(float X, float Z)>() };
            foreach (var c in champions) if (c.Team >= 0) seeing[c.Team].Add((c.X, c.Z));
            foreach (var m in minions) seeing[m.Team].Add((m.X, m.Z));
            foreach (var t in towers) seeing[t.Team].Add((t.X, t.Z));
            bool Visible(int viewer, float x, float z) => seeing[viewer].Any(a => (a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z) <= MinimapVision * MinimapVision);
            int[][][] minionsOf = [[], []];
            for (int side = 0; side < 2; side++)
                minionsOf[side] = [.. minions.Where(m => m.Team == side || Visible(side, m.X, m.Z)).Select(m => new[] { (int)m.X, (int)m.Z, m.Team })];
            int[][] towerRows = [.. towers.Select(t => new[] { (int)t.X, (int)t.Z, t.Team, t.Hp, t.Type })];

            foreach (MatchPlayer mp in match.Players.Values)
            {
                if (server.Players.Find(mp.Id) is not { IsValid: true } viewer) continue;
                int team = TeamIndex(mp.Team);
                if (team < 0) continue;
                Push(viewer, "minimap", new
                {
                    c = champions.Where(c => c.Team == team || (c.Team >= 0 && Visible(team, c.X, c.Z)))
                        .Select(c => new object[] { (int)c.X, (int)c.Z, c.Team, c.Who == mp.Id ? 1 : 0, c.Hp, c.Hero }).ToArray(),
                    m = minionsOf[team],
                    t = towerRows,
                });
            }
        }
    }

    private static int TeamIndex(Faction team) => team switch { Faction.Blue => 0, Faction.Red => 1, _ => -1 };

    private void PushScore(Match match)
    {
        var data = new { blue = match.Score[Faction.Blue], red = match.Score[Faction.Red], seconds = Math.Max(0, (int)(matches.Now - match.StartedAt).TotalSeconds) };
        foreach (MatchPlayer mp in match.Players.Values)
            if (server.Players.Find(mp.Id) is { } player) { Push(player, "score", data); PushMe(match, mp); }
    }

    private object[] ObjectiveTimers(Match match)
    {
        var drake = match.Director?.Mobs.FirstOrDefault(m => ReferenceEquals(m.Kind, BotArena.Ai.Kinds.Bestiary.Drake));
        (string state, int left) d = drake is null ? ("off", 0)
            : drake.Npc is not null && drake.State != MobState.Dead ? ("alive", 0)
            : ("wait", Math.Max(0, (int)Math.Ceiling((drake.RespawnAt - matches.Now).TotalSeconds)));
        var baron = match.Director?.Mobs.FirstOrDefault(m => ReferenceEquals(m.Kind, BotArena.Ai.Kinds.Bestiary.Baron));
        (string state, int left) b = baron is null ? ("off", 0)
            : baron.Npc is not null && baron.State != MobState.Dead ? ("alive", 0)
            : ("wait", Math.Max(0, (int)Math.Ceiling((baron.RespawnAt - matches.Now).TotalSeconds)));
        return
        [
            new { key = "drake", name = "Smok", state = d.state, left = d.left },
            new { key = "baron", name = "Baron", state = b.state, left = b.left },
        ];
    }

    private void PushBoard(Player viewer)
    {
        if (matches.Of(viewer) is not { State: MatchState.Running or MatchState.Finished } match) return;
        var rows = new List<object>();
        foreach (MatchPlayer mp in match.Players.Values)
        {
            Player? who = server.Players.Find(mp.Id) is { IsValid: true } found ? found : null;
            HeroStats? stats = who is null ? null : StatsOf(who);
            rows.Add(new
            {
                id = mp.Id.Value, team = TeamIndex(mp.Team), name = mp.Name, hero = mp.Hero, bot = false, you = mp.Id == viewer.Id,
                lp = who is null ? 0 : profiles.Of(who)?.Lp ?? 0, level = mp.Level, kills = mp.Kills, deaths = mp.Deaths, assists = mp.Assists, cs = mp.BotKills,
                items = mp.Items.ToArray(), ad = stats?.Ad ?? 0, ap = stats?.Ap ?? 0, armor = stats?.Armor ?? 0, resist = stats?.Resist ?? 0,
                alive = who is { IsDead: false } && !mp.Left,
            });
        }
        foreach (MatchBot bot in match.Bots)
        {
            HeroStats stats = HeroCatalog.Find(bot.Hero) is { } hero ? BotArena.Shop.BotShopper.PowerOf(hero, bot).Stats : default;
            bool alive = match.Director?.Mobs.FirstOrDefault(m => m.BotId == bot.Id) is { Npc: not null, State: not MobState.Dead };
            rows.Add(new
            {
                id = bot.Id, team = TeamIndex(bot.Team), name = bot.Name, hero = bot.Hero, bot = true, you = false, lp = 0, level = bot.Level, kills = bot.Kills, deaths = bot.Deaths, assists = bot.Assists, cs = bot.Cs,
                items = bot.Items.ToArray(), ad = stats.Ad, ap = stats.Ap, armor = stats.Armor, resist = stats.Resist, alive,
            });
        }
        Push(viewer, "board", new { rows, seconds = Math.Max(0, (int)(matches.Now - match.StartedAt).TotalSeconds), blue = match.Score[Faction.Blue], red = match.Score[Faction.Red],
            timers = ObjectiveTimers(match) });
    }

    private void PushMe(Match match, MatchPlayer mp)
    {
        if (server.Players.Find(mp.Id) is not { IsValid: true } player) return;
        int level = mp.Level, floor = MatchPlayer.LevelXp[level - 1], next = level < MatchPlayer.LevelXp.Length ? MatchPlayer.LevelXp[level] : MatchPlayer.LevelXp[^1];
        HeroStats? stats = StatsOf(player);
        (int Damage, bool Magic, int CritPercent)? basic = BasicOf(player);
        HeroDef? hero = HeroCatalog.Find(mp.Hero);
        var skills = hero is null ? [] : hero.Abilities.Take(mp.Ranks.Length).Select((a, i) => new
        {
            rank = mp.Ranks[i], max = HeroResources.Rankable(hero, i) ? HeroResources.MaxRank : 0, can = HeroResources.CanRank(hero, i, level, mp.Ranks),
            need = HeroResources.Rankable(hero, i) ? HeroResources.RequiredLevel(hero, i, Math.Min(HeroResources.MaxRank, mp.Ranks[i] + 1)) : 0,
            ult = HeroResources.IsUltimate(hero, i), cost = HeroResources.ManaCost(a, Math.Max(1, mp.Ranks[i])),
        }).ToArray();
        Push(player, "me", new
        {
            points = HeroResources.SkillPoints(level, mp.Ranks), skills,
            kills = mp.Kills, deaths = mp.Deaths, assists = mp.Assists, cs = mp.BotKills, gold = mp.Gold, level, xp = mp.Xp, xpFloor = floor, xpNext = next,
            aa = basic?.Damage ?? 0, aaMagic = basic?.Magic ?? false, critMult = basic?.CritPercent ?? 200,
            ad = stats?.Ad ?? 0, ap = stats?.Ap ?? 0, crit = stats?.Crit ?? 0, armor = stats?.Armor ?? 0, resist = stats?.Resist ?? 0,
            cdr = stats?.Cdr ?? 0, regen = stats?.Regen ?? 0, regenHp = MathF.Round((stats?.Regen ?? 0) / 5f, 1), items = mp.Items.ToArray(),
            charges = mp.Items.Select(k => mp.Charges.GetValueOrDefault(k, 1)).ToArray(),
            elixirs = mp.Elixirs.Select(e => new { key = e.Key, ms = (int)Math.Max(0, e.Value - Environment.TickCount64) }).ToArray(),
        });
    }

    public void PushMana(Player player, int current, int max, float regen) => Push(player, "mana", new { current, max, regen });

    public void Push(Player player, string name, object? data)
    {
        if (player.IsValid) server.Network.Send(player, new UiPushMessage(name, JsonSerializer.Serialize(data, Json)));
    }

    public void Toast(Player player, string text, string kind = "info") => Push(player, "toast", new { text, kind });

    private static string ModeKey(MatchMode mode) => mode switch { MatchMode.Bots => "bots", MatchMode.Ranked => "ranked", _ => "normal" };

    private object ProfileDto(Player player)
    {
        ProfileRecord? p = profiles.Of(player);
        if (p is null) return new { loaded = false, name = player.Name };
        return new
        {
            loaded = true, name = p.Name, points = p.Points, lp = p.Lp, wins = p.Wins, losses = p.Losses, ranked = p.RankedGames,
            rank = profiles.RankOf(player), hero = p.SelectedHero, owned = ProfileService.Owned(p),
        };
    }

    private static object HeroesDto() => HeroCatalog.All.Select(h => new
    {
        key = h.Key, name = h.Name, title = h.Title, role = h.Role, lore = h.Lore, price = h.Price, health = h.Health, strength = h.Strength,
        dexterity = h.Dexterity, weapon = h.WeaponClass.ToString().ToLowerInvariant(), mana = HeroResources.MaxMana(h),
        abilities = h.Abilities.Select((a, slot) => new
        {
            key = a.Key, name = a.Name, text = a.Text, kind = a.Kind.ToString().ToLowerInvariant(), cooldown = MathF.Round(a.Cooldown * HeroResources.CooldownScale, 1), range = a.Range,
            radius = a.Radius, damage = a.Damage, heal = a.Heal, seconds = a.Seconds, cost = HeroResources.ManaCost(a),
            ultimate = HeroResources.IsUltimate(h, slot),
            adRatio = a.AdRatio, apRatio = a.ApRatio, healAdRatio = a.HealAdRatio, healApRatio = a.HealApRatio, magic = HeroResources.IsMagicAbility(h, a),
            ranks = HeroResources.Rankable(h, slot)
                ? Enumerable.Range(1, HeroResources.MaxRank).Select(r => HeroResources.AtRank(a, r) is var x ? new
                {
                    damage = x.Damage, heal = x.Heal, seconds = x.Seconds, cooldown = MathF.Round(x.Cooldown * HeroResources.CooldownScale, 1), cost = HeroResources.ManaCost(a, r), need = HeroResources.RequiredLevel(h, slot, r),
                } : null).ToArray()
                : [],
        }),
    });

    private object RoomsDto() => matches.All.Where(m => m.State != MatchState.Finished || m.Players.Count > 0).OrderByDescending(m => m.IsOpen).ThenBy(m => m.Id).Select(m => new
    {
        id = m.Id, title = m.Title, mode = ModeKey(m.Mode), state = m.State.ToString().ToLowerInvariant(), players = m.Players.Count,
        bots = m.Bots.Count, max = m.MaxPlayers, teamSize = m.TeamSize, host = server.Players.Find(m.Host)?.Name ?? "",
        seconds = m.State == MatchState.Running ? (int)(matches.Now - m.StartedAt).TotalSeconds : 0,
    });

    private object? RoomDto(Match match, Player viewer)
    {
        object Team(Faction team) => match.Players.Values.Where(p => p.Team == team).Select(p => (object)new
        {
            name = p.Name, hero = p.Hero, host = p.Id == match.Host, you = p.Id == viewer.Id, kills = p.Kills, deaths = p.Deaths,
            lp = profiles.Of(server.Players.Find(p.Id) ?? viewer)?.Lp ?? 0, bot = false, id = 0,
        }).Concat(match.Bots.Where(b => b.Team == team).Select(b => (object)new
        {
            name = b.Name, hero = b.Hero, host = false, you = false, kills = b.Kills, deaths = b.Deaths, lp = 0, bot = true, id = b.Id,
        }));
        bool hostRoom = match.State is MatchState.Room or MatchState.Countdown && match.Host == viewer.Id && match.Mode != MatchMode.Ranked;
        return new
        {
            id = match.Id, title = match.Title, mode = ModeKey(match.Mode), state = match.State.ToString().ToLowerInvariant(),
            teamSize = match.TeamSize, isHost = match.Host == viewer.Id, canStart = match.State is MatchState.Room or MatchState.Countdown && match.Host == viewer.Id && match.Mode != MatchMode.Ranked,
            botSlots = new { blue = hostRoom && match.HasBotRoom(Faction.Blue), red = hostRoom && match.HasBotRoom(Faction.Red) },
            countdown = match.State == MatchState.Countdown ? Math.Max(0, (int)Math.Ceiling((match.StartsAt - matches.Now).TotalSeconds)) : 0,
            seconds = match.State == MatchState.Running ? (int)(matches.Now - match.StartedAt).TotalSeconds : 0,
            blue = Team(Faction.Blue), red = Team(Faction.Red), winner = match.Winner?.ToString().ToLowerInvariant(),
        };
    }

    public void PushHud(Player player, string hero) => Push(player, "hud", new { hero });

    public void PushCooldown(Player player, int slot, int ms) => Push(player, "cd", new { slot, ms });

    public void PushProfile(Player player) => Push(player, "profile", ProfileDto(player));

    public void PushQueue(Player player) =>
        Push(player, "queue", new { active = matches.InQueue(player), waiting = matches.QueueLength, since = (int)(matches.Now - matches.QueueSince(player)).TotalSeconds });

    private void PushRoomsToLobby()
    {
        object rooms = RoomsDto();
        foreach (Player player in server.Players) if (player.IsValid && matches.Of(player) is null) Push(player, "rooms", rooms);
    }

    private void PushRoom(Match match)
    {
        foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } player) Push(player, "room", RoomDto(match, player));
    }

    private void PushResults(Match match)
    {
        var best = match.Board.GroupBy(r => r.Team).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Score).First());
        var teams = new[] { Faction.Blue, Faction.Red }.Select(team =>
        {
            Faction enemy = team == Faction.Blue ? Faction.Red : Faction.Blue;
            ResultRow[] members = [.. match.Board.Where(r => r.Team == team)];
            return new
            {
                team = TeamIndex(team), kills = match.Score[team], deaths = members.Sum(r => r.Deaths), assists = members.Sum(r => r.Assists), gold = members.Sum(r => r.Gold),
                damage = members.Sum(r => r.ToChampions), towers = match.TurretsDestroyed[enemy], inhibitors = match.InhibitorsDestroyed[team], drakes = match.DrakeKills[team], barons = match.BaronKills[team],
            };
        }).ToArray();
        foreach (MatchResult result in match.Results)
        {
            if (server.Players.Find(result.Id) is not { } player) continue;
            var rows = match.Board.Select(r => new
            {
                id = r.Id, bot = r.Bot, you = !r.Bot && r.Id == result.Id.Value, team = TeamIndex(r.Team), name = r.Name, hero = r.Hero, level = r.Level, left = r.Left,
                kills = r.Kills, deaths = r.Deaths, assists = r.Assists, cs = r.Cs, gold = r.Gold, dmgChampions = r.ToChampions, dmgObjectives = r.ToObjectives,
                dmgMinions = r.ToMinions, taken = r.Taken, items = r.Items, mvp = best.TryGetValue(r.Team, out ResultRow? top) && ReferenceEquals(top, r),
            }).ToArray();
            LpBreakdown? lp = result.Lp;
            Push(player, "result", new
            {
                won = result.Won, winner = match.Winner?.ToString().ToLowerInvariant(), team = result.Team.ToString().ToLowerInvariant(), hero = result.Hero,
                kills = result.Kills, deaths = result.Deaths, assists = result.Assists, botKills = result.BotKills, points = result.Points, lpChange = result.LpChange,
                lp = result.LpTotal, ranked = match.IsRanked, seconds = (int)(match.EndedAt - match.StartedAt).TotalSeconds,
                mode = match.Mode.ToString().ToLowerInvariant(), rows, teams,
                lpInfo = lp is null ? null : new
                {
                    own = (int)Math.Round(lp.OwnAverage), other = (int)Math.Round(lp.OtherAverage), chance = (int)Math.Round(lp.WinChance * 100), k = lp.K,
                    raw = lp.Raw, limited = lp.Limited, min = lp.Min, max = lp.Max, applied = lp.Applied,
                },
            });
        }
    }

    public void PushAll(Player player)
    {
        Push(player, "heroes", HeroesDto());
        PushProfile(player);
        Push(player, "rooms", RoomsDto());
        PushQueue(player);
        if (matches.Of(player) is { } match) Push(player, "room", RoomDto(match, player)); else Push(player, "room", null);
        RefreshRanking(player);
    }

    private void RefreshRanking(Player player)
    {
        profiles.Top(50, top =>
        {
            ranking = top;
            if (!player.IsValid) return;
            Push(player, "ranking", top.Select((r, i) => new { rank = i + 1, name = r.Name, lp = r.Lp, wins = r.Wins, losses = r.Losses, you = r.Uid == profiles.Of(player)?.Uid }));
        });
    }

    private void Handle(Player player, UiActionMessage message)
    {
        JsonElement args = default;
        try { if (!string.IsNullOrWhiteSpace(message.Json)) args = JsonDocument.Parse(message.Json).RootElement.Clone(); }
        catch (JsonException) { return; }
        string Str(string key) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        double Num(string key, double fallback) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) && v.TryGetDouble(out double n) ? n : fallback;
        int Int(string key, int fallback) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) && v.TryGetInt32(out int n) ? n : fallback;

        if (message.Name.StartsWith("auth.", StringComparison.Ordinal)) { Auth?.Handle(player, message.Name, Str("user"), Str("pass")); return; }
        if (Auth is { } auth && !auth.IsLoggedIn(player) && message.Name is not ("ui.ready" or "client.log"))
        {
            auth.PushState(player);
            return;
        }

        switch (message.Name)
        {
            case "ui.ready":
                if (Auth is { } gate && !gate.IsLoggedIn(player)) gate.PushState(player); else PushAll(player);
                break;
            case "client.log":
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] KLIENT {player.Name}: {Str("t")}";
                server.Log(line);
                if (Directory.Exists("logs")) File.AppendAllText(Path.Combine("logs", "botarena-match.log"), line + Environment.NewLine);
                break;
            }
            case "skill.up":
                SkillUp?.Invoke(player, Int("slot", -1));
                break;
            case "feedback.send":
                Feedback?.Invoke(player, Str("kind"), Int("priority", 2), Str("title"), Str("message"));
                break;
            case "ranking.get":
                RefreshRanking(player);
                break;
            case "room.create":
            {
                MatchMode mode = Str("mode") == "bots" ? MatchMode.Bots : MatchMode.Normal;
                if (matches.Create(player, mode, Int("teamSize", mode == MatchMode.Bots ? 1 : 2), out _) is { } error)
                {
                    Toast(player, error, "error");
                    PushAll(player);
                }
                break;
            }
            case "bot.add":
            {
                Faction team = Str("team") == "red" ? Faction.Red : Faction.Blue;
                if (matches.AddBot(player, team, Str("hero")) is { } botError) Toast(player, botError, "error");
                break;
            }
            case "bot.remove":
                if (matches.RemoveBot(player, Int("id", -1)) is { } removeError) Toast(player, removeError, "error");
                break;
            case "room.join":
                if (matches.Join(player, Int("id", -1)) is { } joinError)
                {
                    Toast(player, joinError, "error");
                    PushAll(player);
                }
                break;
            case "room.leave":
                matches.Leave(player);
                PushAll(player);
                break;
            case "room.team":
                if (matches.SwitchTeam(player) is { } teamError) Toast(player, teamError, "error");
                break;
            case "room.start":
                if (matches.StartRoom(player) is { } startError) Toast(player, startError, "error");
                break;
            case "queue.join":
                if (matches.QueueJoin(player) is { } queueError) Toast(player, queueError, "error");
                break;
            case "queue.leave":
                matches.QueueLeave(player);
                break;
            case "hero.select":
                if (profiles.Select(player, Str("key")) is { } selectError) Toast(player, selectError, "error");
                else matches.HeroChanged(player);
                break;
            case string nav when nav.StartsWith("nav.", StringComparison.Ordinal):
                Nav?.Invoke(player, nav, message.Json);
                break;
            case "board.get":
                PushBoard(player);
                break;
            case "item.open":
            case "item.close":
            case "item.buy":
            case "item.sell":
            case "item.use":
                facing[player.Id] = ((float)Num("dx", 0), (float)Num("dz", 0));
                Shop?.Invoke(player, message.Name, Str("key"), Int("slot", -1));
                break;
            case "item.move":
                Shop?.Invoke(player, message.Name, Int("to", -1).ToString(), Int("from", -1));
                break;
            case "shop.buy":
            {
                string key = Str("key");
                if (profiles.Buy(player, key) is { } buyError) Toast(player, buyError, "error");
                else Toast(player, $"Kupiono: {HeroCatalog.Find(key)?.Name}.", "ok");
                break;
            }
        }
    }
}
