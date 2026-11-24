using BotArena.Heroes;
using BotArena.Persistence.Models;
using BotArena.Profiles;
using BotArena.Shop;

namespace BotArena.Matches;

public sealed class MatchService(Server server, ProfileService profiles, AiDebugFeed debugFeed)
{
    private static readonly TimeSpan CountdownTime = TimeSpan.FromSeconds(60), RankedCountdownTime = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(14);
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan RankedWait = TimeSpan.FromSeconds(8);
    private const int RankedMaxPlayers = 6;

    private sealed record QueueEntry(PlayerId Id, int Lp, TimeSpan Since);

    private readonly Dictionary<int, Match> matches = [];
    private readonly Dictionary<PlayerId, Match> byPlayer = [];
    private readonly List<QueueEntry> queue = [];
    private readonly HashSet<PlayerId> lobby = [];
    private int nextId = 1;
    private TimeSpan now;

    public IEnumerable<Match> All => matches.Values;
    public Match? Of(Player player) => byPlayer.GetValueOrDefault(player.Id);
    public bool InLobby(Player player) => lobby.Contains(player.Id);
    public bool InQueue(Player player) => queue.Any(q => q.Id == player.Id);
    public int QueueLength => queue.Count;
    public TimeSpan QueueSince(Player player) => queue.FirstOrDefault(q => q.Id == player.Id)?.Since ?? TimeSpan.Zero;
    public TimeSpan Now => now;

    public event Action? RoomsChanged;
    public event Action<Match>? RoomChanged;
    public event Action<Player>? QueueChanged;
    public event Action<Match>? Finished;
    public event Action<Match>? ScoreChanged;
    public event Action<Match, MatchPlayer>? StatsChanged;
    public event Action<Player>? LevelUp;
    public const int GoldPerSecond = 2, MinionGold = 22, MinionXp = 25, ChampionGold = 300, ChampionXp = 180;
    public const float MinionXpRadius = 2000f, SharedMinionXp = 1.3032f;
    public static readonly TimeSpan AssistWindow = TimeSpan.FromSeconds(10);
    public const float AssistGoldShare = 0.5f, AssistXpShare = 0.5f;

    public void RefreshStats(Match match, MatchPlayer player) => StatsChanged?.Invoke(match, player);

    private static Mob? BotMob(Match match, MatchBot bot) => match.Director?.Mobs.FirstOrDefault(m => m.BotId == bot.Id);

    private static void RewardBot(Match match, MatchBot bot, int gold, int xp)
    {
        int levelBefore = bot.Level;
        bot.Gold += gold;
        bot.Stats.GoldEarned += Math.Max(0, gold);
        bot.Xp += xp;
        if (bot.Level > levelBefore) ApplyBot(match, bot);
        else if (BotMob(match, bot) is { } mob) mob.WantsShop = BotShopper.CanAffordNext(bot);
    }

    public string? GiveXp(Match match, Faction team, Character ally, int xp)
    {
        if (ally is Player player)
        {
            if (!match.Players.TryGetValue(player.Id, out MatchPlayer? mp) || mp.Team != team) return null;
            Reward(match, mp, 0, xp);
            return mp.Name;
        }
        if (match.Director?.MobOf(ally) is { Champion: true, BotId: { } id } && match.Bots.FirstOrDefault(b => b.Id == id) is { } bot && bot.Team == team)
        {
            RewardBot(match, bot, 0, xp);
            return bot.Name;
        }
        return null;
    }

    private static string BotPlanText(Mob mob)
    {
        try
        {
            ChampionPlan plan = mob.Memory.Get<ChampionPlan>();
            string anchor = plan.Anchor is { } a && mob.Npc is { IsValid: true } npc ? $"kotwica {a.X:0} {a.Z:0} ({npc.Position.DistanceTo(a).Centimeters:0} cm)" : "bez kotwicy";
            return $", {anchor}, tryb \"{plan.Mode}\", recall {plan.Recalling}, retreat {plan.Retreating}, defend {plan.Defending}, porażek marszu {plan.MoveFails}, akcja {mob.CurrentAction?.ToString() ?? "-"}";
        }
        catch (Exception) { return ""; }
    }

    private static void ApplyBot(Match match, MatchBot bot)
    {
        if (HeroCatalog.Find(bot.Hero) is not { } hero || match.Director is not { } director || BotMob(match, bot) is not { } mob) return;
        (HeroStats stats, int health, int damage) = BotShopper.PowerOf(hero, bot);
        if (match.DrakeStacks.GetValueOrDefault(bot.Team) is > 0 and var drakes)
        {
            stats = stats with { Ad = stats.Ad + drakes * DrakeSettings.BuffAd, Ap = stats.Ap + drakes * DrakeSettings.BuffAp };
            damage += drakes * DrakeSettings.BuffAd;
        }
        if (bot.BaronActive)
        {
            stats = stats with { Ad = stats.Ad + bot.BaronAd, Ap = stats.Ap + bot.BaronAp };
            damage += bot.BaronAd;
        }
        director.SetChampionPower(mob, health, damage, stats, bot.Level);
        mob.WantsShop = BotShopper.CanAffordNext(bot);
    }

    private void TickBots(Match match)
    {
        if (LaneSettings.Lanes.Length == 0) return;
        bool log = match.Bots.Count > 0 && now >= match.NextBotLog;
        if (log) match.NextBotLog = now + TimeSpan.FromSeconds(30);
        foreach (MatchBot bot in match.Bots)
        {
            if (log && BotMob(match, bot) is { } watched)
                MatchLog($"BOT {bot.Name}: {(watched.Npc is { IsValid: true } n ? $"poz. {n.Position.X:0} {n.Position.Z:0}" : "brak NPC")}, stan {watched.State}, HP {watched.AuthHealth}/{watched.MaxHealth}, \"{watched.Note}\"{BotPlanText(watched)}");
            if (BotMob(match, bot) is not { } mob) continue;
            bool dead = mob.State == MobState.Dead || mob.Npc is not { IsValid: true, IsDead: false };
            if (!dead) { bot.Gold += GoldPerSecond; bot.Stats.GoldEarned += GoldPerSecond; }
            if (bot.BaronActive && (dead || now >= bot.BaronUntil)) { bot.BaronActive = false; ApplyBot(match, bot); }
            bool atBase = dead || mob.Npc!.Position.DistanceTo(LaneSettings.Lanes[0].SpawnOf(bot.Team)).Centimeters <= BotShopper.ShopRange;
            if (atBase)
            {
                if (BotShopper.Shop(bot)) ApplyBot(match, bot);
                mob.WantsShop = false;
            }
            else mob.WantsShop = BotShopper.CanAffordNext(bot);
        }
    }

    private void Reward(Match match, MatchPlayer player, int gold, int xp)
    {
        int levelBefore = player.Level;
        player.Gold += gold;
        player.Stats.GoldEarned += Math.Max(0, gold);
        player.Xp += xp;
        if (player.Level > levelBefore && server.Players.Find(player.Id) is { IsValid: true } who) LevelUp?.Invoke(who);
        StatsChanged?.Invoke(match, player);
    }

    public event Action<Match, Player, string, GothicOnline.Api.PlayerVisual?, Position>? ChampionSlain;
    public event Action<Player>? BotKilled;
    public event Action<Player, string>? HeroApplied;
    public Func<PlayerId, Player?> RecentAttacker { get; set; } = _ => null;
    public Func<Player, HeroStats?> StatsOf { get; set; } = _ => null;
    public Func<Player, Character, int, int> PlayerHit { get; set; } = (_, _, amount) => amount;
    public Func<Player, int, int> BasicDamage { get; set; } = (_, amount) => amount;
    public Func<Player, float> CritMultiplier { get; set; } = _ => HeroResources.CritMultiplier;
    public Func<Player, bool> BasicMagic { get; set; } = _ => false;
    public Action<Player, TimeSpan> HeroFreeze { get; set; } = (_, _) => { };
    public Action<Player, float, int> HeroSlow { get; set; } = (_, _, _) => { };
    public Action<Character, Mob?, Position, TimeSpan> DragTo { get; set; } = (_, _, _, _) => { };
    public Func<Player, bool, int, float> ShreddedMitigation { get; set; } = (_, _, _) => 1f;
    public Func<Player, float> PlayerShield { get; set; } = _ => 1f;
    public Action<Player, bool> SetLobbyGhost { get; set; } = (_, _) => { };

    public void Start()
    {
        server.Network.Register<ArenaStatusMessage>();
        server.Network.Register<ArenaKillMessage>();
        server.Network.Register<ArenaGoldMessage>();
        server.Network.Register<ArenaDamageMessage>();
        server.Network.Register<ArenaTurretMessage>();
        server.Network.Register<ArenaTurretGoneMessage>();
        server.Network.Register<ArenaTurretShotMessage>();
        server.Network.Register<ArenaMinionShotMessage>();
        server.Network.Register<ArenaMobAniMessage>();
        server.Network.Register<Lobby.ArenaPhaseMessage>();
        server.Events.CharacterDied.Subscribe(e =>
        {
            if (e.Victim is not Player victim || !byPlayer.TryGetValue(victim.Id, out Match? match) || match.State != MatchState.Running) return;
            if (!match.Players.TryGetValue(victim.Id, out MatchPlayer? dead)) return;
            dead.Deaths++;
            StatsChanged?.Invoke(match, dead);
            Character? killer = e.Killer ?? RecentAttacker(victim.Id) ?? match.Director?.RecentAttackerOf(victim);
            CreditKill(match, dead.Team, killer, victim.Position, victimPlayer: victim);
        });
    }

    private void CreditKill(Match match, Faction victimTeam, Character? killer, Position? at = null, Player? victimPlayer = null, Mob? victimBot = null)
    {
        IReadOnlyList<MobDirector.AssistHit> assists = match.Director is { } ledger
            ? victimPlayer is not null ? ledger.TakeAssists(victimPlayer) : victimBot is not null ? ledger.TakeAssists(victimBot) : []
            : [];
        Faction? team = null;
        MatchPlayer? slayer = null;
        MatchBot? bot = null;
        if (match.Director?.MasterOf(killer) is { } master) killer = master;
        if (killer is Player player && match.Players.TryGetValue(player.Id, out slayer)) team = slayer.Team;
        else if (match.Director?.MobOf(killer) is { } mob && mob.Kind.Faction is Faction.Blue or Faction.Red)
        {
            team = mob.Kind.Faction;
            if (mob.BotId is { } id) bot = match.Bots.FirstOrDefault(b => b.Id == id);
        }
        if (team is not { } scoring || scoring == victimTeam) return;
        if (slayer is not null)
        {
            string? victimHero = victimPlayer is not null && match.Players.TryGetValue(victimPlayer.Id, out MatchPlayer? fallenPlayer) ? fallenPlayer.Hero
                : victimBot?.BotId is { } fallenId ? match.Bots.FirstOrDefault(b => b.Id == fallenId)?.Hero : null;
            if (killer is Player slayingPlayer && victimHero is not null) ChampionSlain?.Invoke(match, slayingPlayer, victimHero, victimPlayer?.Visual, at ?? slayingPlayer.Position);
            slayer.Kills++;
            Reward(match, slayer, ChampionGold, ChampionXp);
            ShowGold(slayer, at, ChampionGold);
        }
        if (bot is not null)
        {
            bot.Kills++;
            RewardBot(match, bot, ChampionGold, ChampionXp);
        }
        match.Score[scoring]++;
        ScoreChanged?.Invoke(match);
        PayAssists(match, scoring, assists, slayer, bot, at);
    }

    private void PayAssists(Match match, Faction scoring, IReadOnlyList<MobDirector.AssistHit> assists, MatchPlayer? slayer, MatchBot? slayerBot, Position? at)
    {
        MobDirector.AssistHit[] helpers = [.. assists.Where(a => a.Team == scoring && !(slayer is not null && a.Player == slayer.Id) && !(slayerBot is not null && a.BotId == slayerBot.Id))];
        if (helpers.Length == 0) return;
        int gold = Math.Max(1, (int)(ChampionGold * AssistGoldShare / helpers.Length));
        int xp = Math.Max(1, (int)(ChampionXp * AssistXpShare / helpers.Length));
        foreach (MobDirector.AssistHit helper in helpers)
        {
            if (helper.Player is { } id && match.Players.TryGetValue(id, out MatchPlayer? mp))
            {
                mp.Assists++;
                Reward(match, mp, gold, xp);
                ShowGold(mp, at, gold);
                if (server.Players.Find(mp.Id) is { IsValid: true } player) player.SendMessage(ColorRgb.Gold, $"Asysta! +{gold} złota, +{xp} XP.");
            }
            else if (helper.BotId is { } botId && match.Bots.FirstOrDefault(b => b.Id == botId) is { } helperBot)
            {
                helperBot.Assists++;
                RewardBot(match, helperBot, gold, xp);
            }
        }
    }

    private static void EnterArenaWorld(Player player)
    {
        if (player.World != ArenaSettings.World) player.World = ArenaSettings.World;
    }

    public void EnterLobby(Player player)
    {
        lobby.Add(player.Id);
        player.StreamRadius = ArenaSettings.StreamRadius;
        if (!player.IsDead) player.Health = player.MaxHealth;
        EnterArenaWorld(player);
        player.VirtualWorld = VirtualWorldId.Default;
        player.Position = ArenaSettings.Center;
        SetLobbyGhost(player, true);
        server.Network.Send(player, new Lobby.ArenaPhaseMessage(0, 0));
    }

    public void OnDisconnect(Player player, string? account = null)
    {
        queue.RemoveAll(q => q.Id == player.Id);
        lobby.Remove(player.Id);
        if (account is not null && byPlayer.TryGetValue(player.Id, out Match? match) && match.State == MatchState.Running
            && match.Players.TryGetValue(player.Id, out MatchPlayer? mp))
            away[account] = new Away(match, mp, now + RejoinWindow);
        Leave(player, disconnected: true);
    }

    public static readonly TimeSpan RejoinWindow = TimeSpan.FromMinutes(5);

    private sealed record Away(Match Match, MatchPlayer Player, TimeSpan Until);

    private readonly Dictionary<string, Away> away = [];

    private bool HasAway(Match match) => away.Values.Any(a => a.Match == match);

    public int AwayCount(Match match) => away.Values.Count(a => a.Match == match);

    public event Action<Player>? Rejoined;

    public bool TryRejoin(Player player, string account)
    {
        if (!away.Remove(account, out Away? slot)) return false;
        Match match = slot.Match;
        if (match.State != MatchState.Running || !matches.ContainsKey(match.Id) || byPlayer.ContainsKey(player.Id)) return false;
        MatchPlayer mp = slot.Player;
        mp.Id = player.Id;
        mp.Uid = player.Uid;
        mp.Name = player.Name;
        mp.Left = false;
        mp.Gear.Clear();
        mp.Overlays.Clear();
        match.Players[player.Id] = mp;
        byPlayer[player.Id] = match;
        queue.RemoveAll(q => q.Id == player.Id);
        lobby.Remove(player.Id);
        Teams.Set(player, mp.Team);
        EnterMatch(player, match, mp, 0);
        player.SendMessage(ColorRgb.Gold, "Wróciłeś do trwającej gry.");
        server.Log($"[MATCH #{match.Id}] {player.Name} wrócił do gry");
        Rejoined?.Invoke(player);
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
        ScoreChanged?.Invoke(match);
        return true;
    }

    public string? Create(Player host, MatchMode mode, int teamSize, out Match? created)
    {
        created = null;
        if (mode == MatchMode.Ranked) return RankedEnabled ? "Grę rankingową tworzy kolejka." : RankedLockedText;
        if (profiles.Of(host) is null) return "Profil się wczytuje.";
        if (byPlayer.ContainsKey(host.Id)) return "Już jesteś w grze.";
        queue.RemoveAll(q => q.Id == host.Id);
        int id = nextId++;
        int world = Enumerable.Range(1, 200).First(w => !matches.Values.Any(m => m.World.Value == w));
        string title = $"{host.Name}: {ModeName(mode)}";
        var match = new Match(id, mode, mode == MatchMode.Bots ? Math.Min(teamSize, 3) : Math.Min(teamSize, Match.PlayerTeamSize), new VirtualWorldId(world), title, host.Id) { CreatedAt = now };
        matches[id] = match;
        AddPlayer(match, host, Faction.Blue);
        created = match;
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
        return null;
    }

    public string? Join(Player player, int id)
    {
        if (profiles.Of(player) is null) return "Profil się wczytuje.";
        if (byPlayer.ContainsKey(player.Id)) return "Już jesteś w grze.";
        if (!matches.TryGetValue(id, out Match? match) || match.Mode == MatchMode.Ranked) return "Nie ma takiej gry.";
        if (!match.IsOpen || match.TeamToJoin() is not { } team) return "Gra jest pełna albo już trwa.";
        queue.RemoveAll(q => q.Id == player.Id);
        AddPlayer(match, player, team);
        StartIfFull(match);
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
        return null;
    }

    private void AddPlayer(Match match, Player player, Faction team)
    {
        var mp = new MatchPlayer(player) { Team = team, Hero = profiles.Of(player)?.SelectedHero ?? HeroCatalog.Free.First().Key };
        match.Players[player.Id] = mp;
        byPlayer[player.Id] = match;
        lobby.Remove(player.Id);
    }

    public string? SwitchTeam(Player player)
    {
        if (Of(player) is not { State: MatchState.Room or MatchState.Countdown } match || match.Mode == MatchMode.Bots) return "Nie możesz teraz zmienić drużyny.";
        MatchPlayer mp = match.Players[player.Id];
        Faction other = mp.Team == Faction.Blue ? Faction.Red : Faction.Blue;
        if (match.Total(other) >= match.TeamSize) return "W tamtej drużynie nie ma miejsca.";
        mp.Team = other;
        RoomChanged?.Invoke(match);
        return null;
    }

    public string? AddBot(Player host, Faction team, string heroKey)
    {
        if (Of(host) is not { } match || match.Host != host.Id) return "Tylko gospodarz może dodawać boty.";
        if (match.State is not (MatchState.Room or MatchState.Countdown)) return "Gra już się zaczęła.";
        if (match.Mode == MatchMode.Ranked) return "W grze rankingowej nie ma botów.";
        if (team is not (Faction.Blue or Faction.Red)) return "Nie ma takiej drużyny.";
        if (HeroCatalog.Find(heroKey) is not { } hero) return "Nie ma takiego bohatera.";
        if (!match.HasBotRoom(team)) return "W tej drużynie nie ma miejsca na bota.";
        match.Bots.Add(new MatchBot(match.NextBotId++, team, hero.Key));
        StartIfFull(match);
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
        return null;
    }

    public string? RemoveBot(Player host, int botId)
    {
        if (Of(host) is not { } match || match.Host != host.Id) return "Tylko gospodarz może usuwać boty.";
        if (match.State is not (MatchState.Room or MatchState.Countdown)) return "Gra już się zaczęła.";
        if (match.Bots.RemoveAll(b => b.Id == botId) == 0) return null;
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
        return null;
    }

    public void HeroChanged(Player player)
    {
        if (Of(player) is not { State: MatchState.Room or MatchState.Countdown } match || profiles.Of(player) is not { } profile) return;
        match.Players[player.Id].Hero = profile.SelectedHero;
        RoomChanged?.Invoke(match);
    }

    public string? StartRoom(Player player)
    {
        if (Of(player) is not { } match || match.Host != player.Id) return "Tylko gospodarz może zacząć grę.";
        if (match.IsRanked) return "Grę rankingową startuje serwer.";
        if (match.State == MatchState.Countdown)
        {
            match.StartsAt = now;
            return null;
        }
        if (match.State != MatchState.Room) return "Gra już się zaczęła.";
        if (match.Mode == MatchMode.Normal && (match.Total(Faction.Blue) == 0 || match.Total(Faction.Red) == 0))
            return "W obu drużynach musi być co najmniej jeden gracz albo bot.";
        BeginCountdown(match);
        return null;
    }

    private void StartIfFull(Match match)
    {
        if (match.State == MatchState.Countdown && !match.IsRanked && match.TeamToJoin() is null) match.StartsAt = now;
    }

    private void BeginCountdown(Match match)
    {
        match.State = MatchState.Countdown;
        match.StartsAt = now + (match.IsRanked ? RankedCountdownTime : CountdownTime);
        StartIfFull(match);
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
    }

    public void Leave(Player player, bool disconnected = false)
    {
        if (!byPlayer.Remove(player.Id, out Match? match)) return;
        MatchPlayer? mp = match.Players.GetValueOrDefault(player.Id);
        match.Players.Remove(player.Id);
        if (mp is not null && match.State == MatchState.Running)
        {
            mp.Left = true;
            match.Director?.RetireOwned(player);
            if (match.IsRanked && !disconnected) profiles.Award(player, won: false, 0, PenaltyLp(match, mp), ranked: true);
            else if (match.IsRanked) ForfeitOffline(match, mp);
        }
        if (mp is not null && player.IsValid && !disconnected) ReturnToLobby(player, mp);
        if (match.State == MatchState.Finished) return;
        if (match.Players.Count == 0 && !HasAway(match))
        {
            Dispose(match);
        }
        else
        {
            if (match.Host == player.Id && match.Players.Count > 0) match.Host = match.Players.Keys.First();
            RoomChanged?.Invoke(match);
        }
        RoomsChanged?.Invoke();
    }

    private static string ModeName(MatchMode mode) => mode switch { MatchMode.Bots => "z botami", MatchMode.Ranked => "rankingowa", _ => "z graczami" };

    public static readonly bool RankedEnabled = false;
    public const string RankedLockedText = "Gry rankingowe są w trakcie tworzenia.";

    public string? QueueJoin(Player player)
    {
        if (!RankedEnabled) return RankedLockedText;
        if (profiles.Of(player) is not { } profile) return "Profil się wczytuje.";
        if (byPlayer.ContainsKey(player.Id)) return "Już jesteś w grze.";
        if (InQueue(player)) return null;
        queue.Add(new QueueEntry(player.Id, profile.Lp, now));
        QueueChanged?.Invoke(player);
        return null;
    }

    public void QueueLeave(Player player)
    {
        if (queue.RemoveAll(q => q.Id == player.Id) > 0) QueueChanged?.Invoke(player);
    }

    private void FormRankedMatch()
    {
        if (queue.Count < 2) return;
        QueueEntry oldest = queue.OrderBy(q => q.Since).First();
        bool waited = now - oldest.Since >= RankedWait;
        if (!waited && queue.Count < RankedMaxPlayers) return;
        var group = queue.OrderBy(q => Math.Abs(q.Lp - oldest.Lp)).Take(Math.Min(queue.Count, RankedMaxPlayers)).ToList();
        if (group.Count % 2 == 1) group.RemoveAt(group.Count - 1);
        if (group.Count < 2) return;
        group = [.. group.OrderByDescending(q => q.Lp)];
        int id = nextId++;
        int world = Enumerable.Range(1, 200).First(w => !matches.Values.Any(m => m.World.Value == w));
        var match = new Match(id, MatchMode.Ranked, group.Count / 2, new VirtualWorldId(world), "Rankingowa", group[0].Id) { CreatedAt = now };
        matches[id] = match;
        for (int i = 0; i < group.Count; i++)
        {
            Faction team = (i % 4) is 0 or 3 ? Faction.Blue : Faction.Red;
            if (server.Players.Find(group[i].Id) is { } player) AddPlayer(match, player, team);
            queue.RemoveAll(q => q.Id == group[i].Id);
        }
        foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } p) QueueChanged?.Invoke(p);
        BeginCountdown(match);
    }

    public void FastUpdate(TimeSpan time)
    {
        foreach (Match match in matches.Values)
        {
            if (match.State != MatchState.Running || match.Director is not { } director) continue;
            match.Waves?.Advance(time);
            director.ReleaseHolds(time);
        }
    }

    public void Update(TimeSpan time, IReadOnlyList<CharacterState> snapshot, Func<PlayerId, bool> isGhost)
    {
        now = time;
        FormRankedMatch();
        foreach (var (key, slot) in away.Where(a => time >= a.Value.Until || a.Value.Match.State != MatchState.Running).ToArray())
        {
            away.Remove(key);
            if (slot.Match.IsRanked) profiles.PenalizeOffline("acct:" + key, PenaltyLp(slot.Match, slot.Player));
        }
        foreach (Match match in matches.Values.ToArray())
        {
            if (match.State != MatchState.Finished && !HasAway(match) && !match.Players.Keys.Any(id => server.Players.Find(id) is { IsValid: true }))
            {
                server.Log($"[MATCH #{match.Id}] koniec: brak prawdziwych graczy");
                foreach (PlayerId id in match.Players.Keys.ToArray()) byPlayer.Remove(id);
                match.Players.Clear();
                Dispose(match);
                continue;
            }
            switch (match.State)
            {
                case MatchState.Countdown when now >= match.StartsAt:
                    if (match.Mode == MatchMode.Normal && (match.Total(Faction.Blue) == 0 || match.Total(Faction.Red) == 0))
                    {
                        match.State = MatchState.Room;
                        RoomsChanged?.Invoke();
                        RoomChanged?.Invoke(match);
                    }
                    else Begin(match);
                    break;
                case MatchState.Running:
                    Tick(match, snapshot, isGhost);
                    break;
                case MatchState.Finished when now - match.EndedAt >= ReturnDelay:
                    Dispose(match);
                    break;
            }
        }
    }

    private void Tick(Match match, IReadOnlyList<CharacterState> snapshot, Func<PlayerId, bool> isGhost)
    {
        if (match.Director is not { } director) return;
        var targets = new List<Combatant>();
        foreach (CharacterState state in snapshot)
        {
            if (!state.Exists || !state.IsSpawned || state.IsDead || state.VirtualWorld != match.World) continue;
            if (state.Character is not Player who || !match.Players.ContainsKey(who.Id) || isGhost(who.Id)) continue;
            if (who.IsInvisible) continue;
            targets.Add(new Combatant(who, state.Position, null));
        }
        match.Waves?.Update(now, anyonePlaying: match.Players.Count > 0);
        director.Think(now, targets);
        debugFeed.Publish(director.Mobs, targets);

        if (match.Mode != MatchMode.Bots && (match.Total(Faction.Blue) == 0 || match.Total(Faction.Red) == 0))
        {
            Finish(match, match.Total(Faction.Blue) == 0 ? Faction.Red : Faction.Blue);
            return;
        }
        if (match.CensusStage < 3 && now - match.StartedAt >= TimeSpan.FromSeconds(match.CensusStage switch { 0 => 3, 1 => 12, _ => 30 }))
        {
            match.CensusStage++;
            Census(match, director);
        }
        if (now >= match.NextGoldTick)
        {
            match.NextGoldTick = now + TimeSpan.FromSeconds(1);
            foreach (MatchPlayer mp in match.Players.Values.ToArray())
            {
                if (server.Players.Find(mp.Id) is not { IsValid: true, IsDead: false }) continue;
                mp.Gold += GoldPerSecond;
                mp.Stats.GoldEarned += GoldPerSecond;
                StatsChanged?.Invoke(match, mp);
            }
            TickBots(match);
        }
        if (now >= match.NextScorePush)
        {
            match.NextScorePush = now + TimeSpan.FromSeconds(20);
            ScoreChanged?.Invoke(match);
        }
        if (match.NexusFallen is { } fallen)
        {
            Finish(match, fallen == Faction.Blue ? Faction.Red : Faction.Blue);
            return;
        }
        if (now - match.StartedAt >= TimeLimit)
        {
            float Health(Faction team) => director.Mobs.Where(m => m.Kind.Immobile && m.Kind.Structure != StructureKind.Dragon && m.Kind.Faction == team && m.Npc is not null).Sum(m => (float)m.LastHealth);
            Finish(match, Health(Faction.Blue) >= Health(Faction.Red) ? Faction.Blue : Faction.Red);
        }
    }

    private void Census(Match match, MobDirector director)
    {
        try
        {
            var lines = new List<string>
            {
                $"[{DateTime.Now:HH:mm:ss}] MATCH #{match.Id}: świat wirtualny {match.World.Value}, +{(now - match.StartedAt).TotalSeconds:0} s, mobów {director.Mobs.Count}, botów {match.Bots.Count}",
            };
            foreach (Mob mob in director.Mobs)
            {
                if (mob.Npc is not { IsValid: true } npc) { lines.Add($"  {mob.Label}: BRAK NPC ({mob.Note})"); continue; }
                CharacterState s = server.Snapshot([npc])[0];
                lines.Add($"  {mob.Label}: id {npc.Id.Value}, istnieje {s.Exists}, zespawnowany {s.IsSpawned}, świat {s.World}, wirt. {s.VirtualWorld.Value}, poz. {s.Position.X:0} {s.Position.Y:0} {s.Position.Z:0}, host {mob.Host?.Name ?? "-"}{(mob.Champion ? $", lvl {mob.ChampionLevel}, HP {mob.AuthHealth}/{mob.MaxHealth}, linia {mob.Lane?.Name}, stan {mob.State}, \"{mob.Note}\"{BotPlanText(mob)}" : "")}");
            }
            foreach (MatchPlayer mp in match.Players.Values)
            {
                if (server.Players.Find(mp.Id) is not { IsValid: true } player) continue;
                CharacterState s = server.Snapshot([player])[0];
                string stream;
                try { stream = $"{player.StreamRadius.Centimeters:0}"; } catch (Exception) { stream = "?"; }
                lines.Add($"  GRACZ {mp.Name}: zespawnowany {s.IsSpawned}, świat {s.World}, wirt. {s.VirtualWorld.Value}, poz. {s.Position.X:0} {s.Position.Y:0} {s.Position.Z:0}, promień streamowania {stream} (ustawiany {ArenaSettings.StreamRadius.Centimeters:0})");
            }
            foreach (string line in lines) server.Log(line);
            if (Directory.Exists("logs")) File.AppendAllLines(Path.Combine("logs", "botarena-match.log"), lines);
        }
        catch (Exception error) { server.Log($"census: {error.Message}"); }
    }

    private void Begin(Match match)
    {
        match.State = MatchState.Running;
        match.StartedAt = now;
        var director = new MobDirector(server, seed: 1000 + match.Id, store: null, match.World);
        match.Director = director;
        match.Waves = new WaveSpawner(director);
        director.Started = now;
        director.Mitigation = (player, magic) => StatsOf(player) is { } stats ? HeroResources.Mitigation(magic ? stats.Resist : stats.Armor) : 1f;
        director.ShreddedMitigation = (player, magic, shred) => ShreddedMitigation(player, magic, shred);
        director.PlayerShield = player => PlayerShield(player);
        director.HeroFreeze = (player, until) => HeroFreeze(player, until);
        director.HeroSlow = (player, scale, millis) => HeroSlow(player, scale, millis);
        director.DragTo = (who, mob, to, duration) => DragTo(who, mob, to, duration);
        director.GroundFx += (_, at, fx) => Send(match, new AbilityGroundMessage(-1, fx, at, 0));
        director.CritRoll = player => StatsOf(player) is { } stats && Random.Shared.Next(100) < stats.Crit;
        director.PlayerHit = (player, victim, amount) => PlayerHit(player, victim, amount);
        director.BasicDamage = (player, amount) => BasicDamage(player, amount);
        director.CritMultiplier = player => CritMultiplier(player);
        director.BasicMagic = player => BasicMagic(player);
        director.ChampionMitigation = (mob, magic) =>
            mob.ChampionStats is { } own ? HeroResources.Mitigation(magic ? own.Resist : own.Armor)
            : match.Bots.FirstOrDefault(b => b.Id == mob.BotId) is { } bot && HeroCatalog.Find(bot.Hero) is { } botHero
                ? HeroResources.Mitigation(magic ? HeroResources.StatsOf(botHero, 1).Resist : HeroResources.StatsOf(botHero, 1).Armor) : 1f;
        director.PopulationChanged += () => Send(match, new ArenaStatusMessage(director.Alive, director.Total));
        director.MobKilled += (mob, killer) => OnMobKilled(match, mob, killer);
        director.DamageDealt += (attacker, victim, amount, magic) => CountDamage(match, attacker, victim, amount, magic);
        director.TurretSpawned += mob => { if (mob.Kind.Structure == StructureKind.Turret) match.TurretsBuilt[mob.Kind.Faction]++; Send(match, TurretMessage(mob)); };
        director.StructureRebuilt += mob => Announce(match, mob.Kind.Faction, $"{mob.Kind.DisplayName} odbudowany!");
        director.ObjectiveSpawned += mob =>
        {
            foreach (MatchPlayer mp in match.Players.Values)
                if (server.Players.Find(mp.Id) is { } p) p.SendMessage(ColorRgb.Gold, $"{mob.Kind.DisplayName} pojawił się w swojej jamie!");
            server.Log($"[MATCH #{match.Id}] {mob.Kind.DisplayName}: pojawił się");
        };
        director.HitBlocked += (player, mob) => HintProtected(player, mob);
        director.TurretRemoved += npc => Send(match, new ArenaTurretGoneMessage(npc));
        director.TurretShot += (turret, target) =>
        {
            if (turret.Npc is { IsValid: true } npc && target.IsValid) Send(match, new ArenaTurretShotMessage(npc.Id.Value, MobCommands.IdOf(target)));
        };
        director.MinionShot += (caster, target) =>
        {
            if (caster.Npc is { IsValid: true } npc && target.IsValid)
                Send(match, new ArenaMinionShotMessage(npc.Id.Value, MobCommands.IdOf(target), caster.Kind.Faction == Faction.Blue ? 0 : 1,
                    caster.Behaviour.Get<BoltCombat>() is { Heavy: true }));
        };
        director.MobAnimation += (mob, animation) =>
        {
            if (mob.Npc is { IsValid: true } npc) Send(match, new ArenaMobAniMessage(npc.Id.Value, animation));
        };
        director.ChampionAuraFx += (caster, fx, millis) =>
        {
            if (caster.Npc is { IsValid: true } npc) Send(match, new AbilityAuraMessage(npc.Id.Value, fx, millis));
        };
        director.ChampionFx += (caster, target, kind, fx) =>
        {
            if (caster.Npc is { IsValid: true } npc)
                Send(match, new AbilityFxMessage(npc.Id.Value, target is { IsValid: true } ? MobCommands.IdOf(target) : -1, kind, fx));
        };
        director.CreateCamps(now);
        director.CreateTurrets(now);
        int index = 0;
        foreach (MatchPlayer mp in match.Players.Values.ToArray())
        {
            if (server.Players.Find(mp.Id) is not { IsValid: true } player) continue;
            mp.StartLp = profiles.Of(player)?.Lp ?? 0;
            Teams.Set(player, mp.Team);
            EnterMatch(player, match, mp, index++);
        }
        SpawnBots(match, director);
        int towers = director.Mobs.Count(m => m.Kind.Immobile && m.Npc is not null);
        string summary = $"Gra #{match.Id}: linii {LaneSettings.Lanes.Length}, wież {towers}, botów-bohaterów {match.Bots.Count}; pierwsza fala minionów za {LaneSettings.FirstWave.TotalSeconds:0} s.";
        foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } p) p.SendMessage(ColorRgb.Gold, summary);
        match.NextScorePush = now + TimeSpan.FromSeconds(20);
        ScoreChanged?.Invoke(match);
        server.Log($"[MATCH #{match.Id}] start: {ModeName(match.Mode)}, {match.Players.Count} graczy, {match.Bots.Count} botów, świat {match.World.Value}");
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
    }

    private void SpawnBots(Match match, MobDirector director)
    {
        if (!LaneSettings.Enabled || LaneSettings.Lanes.Length == 0) return;
        foreach (Faction team in (Faction[])[Faction.Blue, Faction.Red])
        {
            int index = match.Count(team);
            int laneIndex = 0;
            foreach (MatchBot bot in match.Bots.Where(b => b.Team == team))
            {
                if (HeroCatalog.Find(bot.Hero) is not { } hero) continue;
                LaneDef lane = LaneSettings.Lanes[laneIndex++ % LaneSettings.Lanes.Length];
                Position spawn = lane.SpawnOf(team);
                float side = (index % 3 - 1) * 180, back = (index / 3) * 160;
                director.SpawnChampion(ChampionKinds.Of(hero, team), lane, new Position(spawn.X + side, spawn.Y + 40, spawn.Z + back), now, bot.Id);
                BotShopper.Start(bot, hero);
                ApplyBot(match, bot);
                index++;
            }
        }
    }

    private void EnterMatch(Player player, Match match, MatchPlayer mp, int index)
    {
        SetLobbyGhost(player, false);
        if (player.IsDead) player.Respawn();
        EnterArenaWorld(player);
        player.VirtualWorld = match.World;
        HeroDef hero = HeroCatalog.Find(mp.Hero) ?? HeroCatalog.Knight;
        if (!player.IsSpawned) player.Spawn();
        HeroSetup.Apply(player, hero, mp);
        HeroApplied?.Invoke(player, mp.Hero);
        PlaceAtBase(player, mp, index);
        server.Network.Send(player, new Lobby.ArenaPhaseMessage(1, match.Id));
        if (match.Director is { } director)
        {
            server.Network.Send(player, new ArenaStatusMessage(director.Alive, director.Total));
            foreach (Mob turret in director.Mobs)
                if (turret.Kind.Immobile && turret.Npc is { IsValid: true }) server.Network.Send(player, TurretMessage(turret));
        }
    }

    public static Position? BaseOf(MatchPlayer mp) => LaneSettings.Lanes[0].SpawnOf(mp.Team);

    public void TeleportToBase(Player player)
    {
        if (Of(player) is { } match && match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) PlaceAtBase(player, mp, 0);
    }

    private static void PlaceAtBase(Player player, MatchPlayer mp, int index)
    {
        Position spawn = LaneSettings.Lanes[0].SpawnOf(mp.Team);
        float side = (index % 3 - 1) * 180, back = (index / 3) * 160;
        player.Position = new Position(spawn.X + side, spawn.Y + 40, spawn.Z + back);
    }

    public void OnRespawn(Player player)
    {
        if (Of(player) is not { State: MatchState.Running or MatchState.Finished } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return;
        HeroDef hero = HeroCatalog.Find(mp.Hero) ?? HeroCatalog.Knight;
        MatchLog($"RESPAWN {player.Name}: start, świat wirt. {player.VirtualWorld.Value} (gra {match.World.Value}), zespawnowany {player.IsSpawned}");
        EnterArenaWorld(player);
        if (!player.IsSpawned) player.Spawn();
        HeroSetup.Apply(player, hero, mp);
        HeroApplied?.Invoke(player, mp.Hero);
        PlaceAtBase(player, mp, 0);
        RestreamAfterRespawn(player, match);
    }

    private void RestreamAfterRespawn(Player player, Match match)
    {
        PlayerId id = player.Id;
        bool Alive(out Player who)
        {
            who = server.Players.Find(id)!;
            return who is { IsValid: true } && Of(who) == match && match.State is MatchState.Running or MatchState.Finished;
        }
        player.VirtualWorld = VirtualWorldId.Default;
        server.Run(async () =>
        {
            await server.Delay(TimeSpan.FromMilliseconds(300));
            if (!Alive(out Player who)) return;
            who.VirtualWorld = match.World;
            MatchLog($"RESPAWN {who.Name}: powrót do świata gry {match.World.Value} (jest {who.VirtualWorld.Value})");

            await server.Delay(TimeSpan.FromSeconds(1.2));
            if (!Alive(out who)) return;
            if (who.VirtualWorld != match.World) who.VirtualWorld = match.World;
            (int near, int mine, int orphans) = Streaming(who, match);
            MatchLog($"RESPAWN {who.Name}: po 1,5 s świat wirt. {who.VirtualWorld.Value}, zespawnowany {who.IsSpawned}, NPC gry w 35 m: {near}, hostuje je on: {mine}, bez hosta: {orphans}");
            if (!who.IsSpawned || (near > 0 && mine == 0 && orphans == near))
            {
                Position at = who.Position;
                who.Unspawn();
                await server.Delay(TimeSpan.FromMilliseconds(200));
                if (!Alive(out who)) return;
                who.Spawn();
                who.Position = at;
                who.VirtualWorld = match.World;
                MatchLog($"RESPAWN {who.Name}: brak NPC — gracz zespawnowany ponownie");
            }

            await server.Delay(TimeSpan.FromSeconds(3));
            if (!Alive(out who)) return;
            (near, mine, orphans) = Streaming(who, match);
            MatchLog($"RESPAWN {who.Name}: po 5 s świat wirt. {who.VirtualWorld.Value}, zespawnowany {who.IsSpawned}, NPC gry w 35 m: {near}, hostuje je on: {mine}, bez hosta: {orphans}");
        });
    }

    private static (int Near, int Mine, int Orphans) Streaming(Player player, Match match)
    {
        if (match.Director is not { } director) return (0, 0, 0);
        Position at = player.Position;
        int near = 0, mine = 0, orphans = 0;
        foreach (Mob mob in director.Mobs)
        {
            if (mob.Npc is not { IsValid: true } npc || npc.IsDead || npc.Position.DistanceTo(at).Centimeters > 3500) continue;
            near++;
            Player? host = npc.Host;
            if (host is null) orphans++;
            else if (host.Id == player.Id) mine++;
        }
        return (near, mine, orphans);
    }

    private static void MatchLog(string line)
    {
        string stamped = $"[{DateTime.Now:HH:mm:ss}] {line}";
        Console.WriteLine(stamped);
        try { if (Directory.Exists("logs")) File.AppendAllLines(Path.Combine("logs", "botarena-match.log"), [stamped]); }
        catch (IOException) { }
    }

    private static DamageStats? StatsOfFighter(Match match, Character who) =>
        who is Player player ? (match.Players.TryGetValue(player.Id, out MatchPlayer? mp) ? mp.Stats : null)
        : match.Director?.MobOf(who) is { BotId: { } id } ? match.Bots.FirstOrDefault(b => b.Id == id)?.Stats : null;

    private static Faction? TeamOfFighter(Match match, Character who) =>
        who is Player player ? (match.Players.TryGetValue(player.Id, out MatchPlayer? mp) ? mp.Team : null)
        : match.Director?.MobOf(who) is { Kind.Faction: Faction.Blue or Faction.Red } mob ? mob.Kind.Faction : null;

    private static string? FighterKey(Match match, Character who) =>
        who is Player player ? (match.Players.ContainsKey(player.Id) ? "p" + player.Id.Value : null)
        : match.Director?.MobOf(who) is { BotId: { } id } ? "b" + id : null;

    private void PayTowerGold(Match match, Mob tower)
    {
        Faction scoring = tower.Kind.Faction == Faction.Blue ? Faction.Red : Faction.Blue;
        Position at = tower.Home;
        string name = tower.Kind.DisplayName;
        if (!match.TowerHits.Remove(tower, out Dictionary<string, TimeSpan>? hits)) hits = [];
        bool Recent(string key) => hits.TryGetValue(key, out TimeSpan when) && now - when <= LaneSettings.TowerGoldWindow;
        var players = new List<(MatchPlayer Slot, Player Who)>();
        var excluded = new List<(Player Who, string Why)>();
        foreach (MatchPlayer mp in match.Players.Values)
        {
            if (mp.Team != scoring || server.Players.Find(mp.Id) is not { IsValid: true } who) continue;
            if (!hits.ContainsKey("p" + mp.Id.Value)) continue;
            if (!Recent("p" + mp.Id.Value)) excluded.Add((who, $"ostatnio biłeś ją ponad {LaneSettings.TowerGoldWindow.TotalSeconds:0} s temu"));
            else if (who.IsDead) excluded.Add((who, "byłeś martwy"));
            else if (who.Position.DistanceTo(at).Centimeters > LaneSettings.TowerGoldRadius)
                excluded.Add((who, $"stałeś dalej niż {LaneSettings.TowerGoldRadius / 100:0} m od niej"));
            else players.Add((mp, who));
        }
        var bots = new List<MatchBot>();
        foreach (MatchBot bot in match.Bots)
            if (bot.Team == scoring && Recent("b" + bot.Id) && BotMob(match, bot) is { Npc: { IsValid: true, IsDead: false } npc }
                && npc.Position.DistanceTo(at).Centimeters <= LaneSettings.TowerGoldRadius) bots.Add(bot);
        int count = players.Count + bots.Count;
        int share = count == 0 ? 0 : Math.Max(1, LaneSettings.TowerGold / count);
        foreach ((MatchPlayer slot, Player who) in players)
        {
            Reward(match, slot, share, 0);
            ShowGold(slot, who.Position, share);
            who.SendMessage(ColorRgb.Gold, $"{name} zniszczona! +{share} złota ({LaneSettings.TowerGold} dzielone na {count}).");
        }
        foreach (MatchBot bot in bots) RewardBot(match, bot, share, 0);
        foreach ((Player who, string why) in excluded)
            who.SendMessage(ColorRgb.Gold, $"{name} zniszczona, ale nie dostajesz złota: {why}.");
        if (count == 0)
            foreach (MatchPlayer mp in match.Players.Values)
                if (mp.Team == scoring && excluded.All(e => e.Who.Id != mp.Id) && server.Players.Find(mp.Id) is { IsValid: true } silent)
                    silent.SendMessage(ColorRgb.Gold, $"{name} zniszczona, ale nikt nie dostał złota (dostają je tylko ci, którzy bili wieżę w ciągu {LaneSettings.TowerGoldWindow.TotalSeconds:0} s i stoją przy niej).");
        MatchLog($"GRA #{match.Id}: {name} zniszczona, zapisanych ciosów: {hits.Count}, nagrodzonych: {count} (po {share} zł), odrzuconych: {excluded.Count}"
            + (excluded.Count > 0 ? " [" + string.Join("; ", excluded.Select(e => $"{e.Who.Name}: {e.Why}")) + "]" : ""));
    }

    private void ShowDamage(Match match, Character attacker, Character victim, int amount, bool magic)
    {
        int type = magic ? 1 : 0;
        try
        {
            if (attacker is Player dealer && dealer.IsValid && match.Players.ContainsKey(dealer.Id) && victim.IsValid)
                server.Network.Send(dealer, new ArenaDamageMessage(victim.Position, amount, type, false), GothicOnline.Api.PacketReliability.UnreliableSequenced);
            if (victim is Player taker && taker.IsValid && match.Players.ContainsKey(taker.Id))
                server.Network.Send(taker, new ArenaDamageMessage(taker.Position, amount, type, true), GothicOnline.Api.PacketReliability.UnreliableSequenced);
        }
        catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException) {  }
    }

    private void CountDamage(Match match, Character attacker, Character victim, int amount, bool magic)
    {
        if (match.State != MatchState.Running || amount <= 0) return;
        if (TeamOfFighter(match, attacker) is { } attackerTeam && TeamOfFighter(match, victim) is { } victimTeam && attackerTeam == victimTeam) return;
        if (StatsOfFighter(match, victim) is { } taker) taker.Taken += amount;
        ShowDamage(match, attacker, victim, amount, magic);
        Mob? victimMob = match.Director?.MobOf(victim);
        if (victimMob is { Kind.Structure: StructureKind.Turret } tower && FighterKey(match, attacker) is { } key)
        {
            if (!match.TowerHits.TryGetValue(tower, out Dictionary<string, TimeSpan>? hits)) match.TowerHits[tower] = hits = [];
            hits[key] = now;
        }
        if (StatsOfFighter(match, attacker) is not { } dealer) return;
        if (victim is Player || victimMob is { Champion: true }) dealer.ToChampions += amount;
        else if (victimMob is { Kind.Immobile: true } || victimMob?.Kind.Key == Bestiary.Drake.Key || victimMob?.Kind.Key == Bestiary.Baron.Key) dealer.ToObjectives += amount;
        else dealer.ToMinions += amount;
    }

    private void OnMobKilled(Match match, Mob mob, Character? killer)
    {
        if (mob.IsHelper) return;
        if (match.Director?.MasterOf(killer) is { } master) killer = master;
        if (mob.Kind.Key == Bestiary.Drake.Key)
        {
            DrakeSlain(match, mob, killer);
            return;
        }
        if (mob.Kind.Key == Bestiary.Baron.Key)
        {
            BaronSlain(match, mob, killer);
            return;
        }
        if (mob.Kind.Immobile)
        {
            if (mob.Kind.Structure == StructureKind.Turret) { match.TurretsDestroyed[mob.Kind.Faction]++; PayTowerGold(match, mob); }
            if (mob.Kind.Structure == StructureKind.Inhibitor && mob.Kind.Faction is Faction.Blue or Faction.Red) match.InhibitorsDestroyed[mob.Kind.Faction == Faction.Blue ? Faction.Red : Faction.Blue]++;
            string text = mob.Kind.Structure switch
            {
                StructureKind.Inhibitor => $"{mob.Kind.DisplayName} zniszczony! Odbuduje się za {LaneSettings.InhibitorRespawn.TotalMinutes:0} min — wtedy nexus znów będzie chroniony.",
                StructureKind.Nexus => $"{mob.Kind.DisplayName} runął!",
                _ => $"{mob.Kind.DisplayName} zniszczona!",
            };
            Announce(match, mob.Kind.Faction, text);
            if (mob.Kind.Structure == StructureKind.Nexus) match.NexusFallen ??= mob.Kind.Faction;
            return;
        }
        if (mob.Champion)
        {
            if (mob.BotId is { } id && match.Bots.FirstOrDefault(b => b.Id == id) is { } fallen) fallen.Deaths++;
            CreditKill(match, mob.Kind.Faction, killer, mob.LastPosition, victimBot: mob);
            return;
        }
        int xp = mob.Kind.Xp ?? MinionXp;
        bool shared = mob.Lane is not null;
        if (shared) ShareMinionXp(match, mob, xp);
        if (match.Director?.MobOf(killer) is { BotId: { } botId } && match.Bots.FirstOrDefault(b => b.Id == botId) is { } farmer)
        {
            farmer.Cs++;
            RewardBot(match, farmer, mob.Kind.Gold ?? MinionGold, shared ? 0 : xp);
            return;
        }
        if (killer is not Player { IsValid: true } player || !match.Players.TryGetValue(player.Id, out MatchPlayer? slayer)) return;
        slayer.BotKills++;
        int gold = mob.Kind.Gold ?? MinionGold;
        Reward(match, slayer, gold, shared ? 0 : xp);
        ShowGold(slayer, mob.LastPosition, gold);
        if (mob.Kind.Buff != JungleBuff.None) GrantJungleBuff(match, slayer, player, mob.Kind);
        BotKilled?.Invoke(player);
        server.Network.Send(player, new ArenaKillMessage(mob.Kind.DisplayName, slayer.BotKills));
    }

    private void ShareMinionXp(Match match, Mob mob, int xp)
    {
        if (xp <= 0 || mob.Kind.Faction is not (Faction.Blue or Faction.Red)) return;
        Faction enemy = mob.Kind.Faction == Faction.Blue ? Faction.Red : Faction.Blue;
        Position at = mob.LastPosition;
        bool Near(Position p) => p.DistanceTo(at).Centimeters <= MinionXpRadius;
        var players = match.Players.Values
            .Where(mp => mp.Team == enemy && server.Players.Find(mp.Id) is { IsValid: true, IsDead: false } who && Near(who.Position)).ToList();
        var bots = match.Bots
            .Where(b => b.Team == enemy && BotMob(match, b) is { Npc: { IsValid: true, IsDead: false } npc } && Near(npc.Position)).ToList();
        int count = players.Count + bots.Count;
        if (count == 0) return;
        int each = count == 1 ? xp : Math.Max(1, (int)MathF.Round(xp * SharedMinionXp / count));
        foreach (MatchPlayer mp in players) Reward(match, mp, 0, each);
        foreach (MatchBot bot in bots) RewardBot(match, bot, 0, each);
    }

    private void DrakeSlain(Match match, Mob drake, Character? killer)
    {
        Faction? team = killer is Player player && match.Players.TryGetValue(player.Id, out MatchPlayer? slayer) ? slayer.Team
            : match.Director?.MobOf(killer) is { Kind.Faction: Faction.Blue or Faction.Red } mob ? mob.Kind.Faction : null;
        if (team is not { } winner)
        {
            foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } p) p.SendMessage(ColorRgb.Gold, $"{drake.Kind.DisplayName} padł — nikt go nie zdobył.");
            return;
        }
        match.DrakeKills[winner]++;
        int stacks = match.DrakeStacks[winner] = Math.Min(DrakeSettings.MaxStacks, match.DrakeStacks[winner] + 1);
        foreach (MatchPlayer member in match.Players.Values.Where(p => p.Team == winner).ToArray())
        {
            Reward(match, member, DrakeSettings.Gold, DrakeSettings.Xp);
            ShowGold(member, drake.LastPosition, DrakeSettings.Gold);
        }
        foreach (MatchBot bot in match.Bots.Where(b => b.Team == winner))
        {
            RewardBot(match, bot, DrakeSettings.Gold, DrakeSettings.Xp);
            ApplyBot(match, bot);
        }
        string who = winner == Faction.Blue ? "Niebiescy" : "Czerwoni";
        string text = $"{who} zabili smoka! Smocza Moc x{stacks}: +{stacks * DrakeSettings.BuffAd} AD, +{stacks * DrakeSettings.BuffAp} AP, +{DrakeSettings.Gold} złota. " +
                      $"Smok wróci za {DrakeSettings.Respawn.TotalMinutes:0} min.";
        Announce(match, winner, text);
        server.Log($"[MATCH #{match.Id}] smok rzeczny: {who}, stosów {stacks}");
    }

    private void BaronSlain(Match match, Mob baron, Character? killer)
    {
        Faction? team = killer is Player player && match.Players.TryGetValue(player.Id, out MatchPlayer? slayer) ? slayer.Team
            : match.Director?.MobOf(killer) is { Kind.Faction: Faction.Blue or Faction.Red } mob ? mob.Kind.Faction : null;
        if (team is not { } winner)
        {
            foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } p) p.SendMessage(ColorRgb.Gold, $"{baron.Kind.DisplayName} padł — nikt go nie zdobył.");
            return;
        }
        match.BaronKills[winner]++;
        (int ad, int ap) = BaronSettings.BuffAt(now - match.StartedAt);
        TimeSpan until = now + TimeSpan.FromSeconds(BaronSettings.BuffSeconds);
        long untilTick = Environment.TickCount64 + BaronSettings.BuffSeconds * 1000L;
        foreach (MatchPlayer member in match.Players.Values.Where(p => p.Team == winner).ToArray())
        {
            bool finisher = killer is Player k && k.Id == member.Id;
            int gold = BaronSettings.TeamGold + (finisher ? BaronSettings.KillerGold : 0);
            Reward(match, member, gold, BaronSettings.TeamXp);
            if (finisher) ShowGold(member, baron.LastPosition, gold);
            if (server.Players.Find(member.Id) is { IsValid: true, IsDead: false } living)
            {
                member.BaronAd = ad;
                member.BaronAp = ap;
                member.Elixirs[BaronSettings.BuffKey] = untilTick;
                living.SendMessage(ColorRgb.Gold, $"Ręka Barona: +{ad} AD, +{ap} AP, wzmocnione miniony przez {BaronSettings.BuffSeconds} s (znika po śmierci).");
                RefreshStats(match, member);
            }
        }
        MatchBot? finisherBot = match.Director?.MobOf(killer) is { BotId: { } botId } ? match.Bots.FirstOrDefault(b => b.Id == botId) : null;
        foreach (MatchBot bot in match.Bots.Where(b => b.Team == winner))
        {
            int gold = BaronSettings.TeamGold + (ReferenceEquals(bot, finisherBot) ? BaronSettings.KillerGold : 0);
            RewardBot(match, bot, gold, BaronSettings.TeamXp);
            bool alive = BotMob(match, bot) is { } botMob && botMob.State != MobState.Dead && botMob.Npc is { IsValid: true, IsDead: false };
            if (alive) { bot.BaronActive = true; bot.BaronUntil = until; bot.BaronAd = ad; bot.BaronAp = ap; }
            ApplyBot(match, bot);
        }
        match.Director!.EmpoweredUntil[winner] = until;
        string who = winner == Faction.Blue ? "Niebiescy" : "Czerwoni";
        string text = $"{who} zabili Barona Nashora! Ręka Barona: +{ad} AD, +{ap} AP i wzmocnione miniony przez {BaronSettings.BuffSeconds} s, +{BaronSettings.TeamGold} złota " +
                      $"i +{BaronSettings.TeamXp} XP dla każdego. Baron wróci za {BaronSettings.Respawn.TotalMinutes:0} min.";
        Announce(match, winner, text);
        server.Log($"[MATCH #{match.Id}] baron: {who}, AD +{ad}, AP +{ap}");
    }

    private void GrantJungleBuff(Match match, MatchPlayer mp, Player player, MobKind kind)
    {
        string key = kind.Buff == JungleBuff.Blue ? JungleSettings.BlueKey : JungleSettings.RedKey;
        mp.Elixirs[key] = Environment.TickCount64 + JungleSettings.BuffSeconds * 1000L;
        string effect = kind.Buff == JungleBuff.Blue
            ? $"regeneracja many x{1 + JungleSettings.BlueManaRegen / 100}"
            : $"twoje ataki podpalają ({JungleSettings.BurnDamage} obrażeń przez {JungleSettings.BurnSeconds} s)";
        player.SendMessage(ColorRgb.Gold, $"{ItemCatalog.ElixirName(key)}: {effect} przez {JungleSettings.BuffSeconds} s (znika po śmierci).");
        RefreshStats(match, mp);
    }

    private void ShowGold(MatchPlayer slayer, Position? at, int gold)
    {
        if (at is { } position && gold > 0 && server.Players.Find(slayer.Id) is { IsValid: true } player)
            server.Network.Send(player, new ArenaGoldMessage(position, gold));
    }

    private void Announce(Match match, Faction team, string text)
    {
        ColorRgb color = team == Faction.Blue ? ColorRgb.FromRgb(120, 160, 255) : ColorRgb.FromRgb(255, 110, 110);
        foreach (MatchPlayer mp in match.Players.Values) if (server.Players.Find(mp.Id) is { } p) p.SendMessage(color, text);
    }

    private readonly Dictionary<PlayerId, TimeSpan> lastHint = [];

    private void HintProtected(Player player, Mob mob)
    {
        if (lastHint.TryGetValue(player.Id, out TimeSpan last) && now - last < TimeSpan.FromSeconds(4)) return;
        lastHint[player.Id] = now;
        player.SendMessage(ColorRgb.Gold, MobDirector.ProtectionHint(mob));
    }

    private static ArenaTurretMessage TurretMessage(Mob mob) =>
        new(mob.Npc!.Id.Value, mob.Kind.DisplayName, mob.Home, mob.Kind.Faction == Faction.Blue ? 0 : 1, mob.Kind.Health);

    public void Send<T>(Match match, T message, GothicOnline.Api.PacketReliability reliability = GothicOnline.Api.PacketReliability.ReliableOrdered) where T : notnull
    {
        foreach (MatchPlayer mp in match.Players.Values)
            if (server.Players.Find(mp.Id) is { IsValid: true } player) server.Network.Send(player, message, reliability);
    }

    private static double PerformanceScore(int kills, int deaths, int assists, int cs, DamageStats stats) =>
        kills * 3.0 + assists * 1.5 - deaths + stats.ToChampions / 600.0 + stats.ToObjectives / 1500.0 + cs * 0.05;

    private static void BuildBoard(Match match)
    {
        match.Board.Clear();
        foreach (MatchPlayer mp in match.Players.Values)
            match.Board.Add(new ResultRow(mp.Id.Value, false, mp.Name, mp.Hero, mp.Team, mp.Left, mp.Level, mp.Kills, mp.Deaths, mp.Assists, mp.BotKills,
                mp.Stats.GoldEarned, mp.Stats.ToChampions, mp.Stats.ToObjectives, mp.Stats.ToMinions, mp.Stats.Taken, [.. mp.Items], PerformanceScore(mp.Kills, mp.Deaths, mp.Assists, mp.BotKills, mp.Stats)));
        foreach (MatchBot bot in match.Bots)
            match.Board.Add(new ResultRow(bot.Id, true, bot.Name, bot.Hero, bot.Team, false, bot.Level, bot.Kills, bot.Deaths, bot.Assists, bot.Cs,
                bot.Stats.GoldEarned, bot.Stats.ToChampions, bot.Stats.ToObjectives, bot.Stats.ToMinions, bot.Stats.Taken, [.. bot.Items], PerformanceScore(bot.Kills, bot.Deaths, bot.Assists, bot.Cs, bot.Stats)));
    }

    private void Finish(Match match, Faction winner)
    {
        if (match.State == MatchState.Finished) return;
        match.State = MatchState.Finished;
        match.EndedAt = now;
        match.Winner = winner;
        match.Waves = null;
        BuildBoard(match);
        double LpOf(Faction team)
        {
            MatchPlayer[] members = [.. match.Players.Values.Where(p => p.Team == team)];
            return members.Length == 0 ? 0 : members.Average(p => (double)p.StartLp);
        }
        foreach (MatchPlayer mp in match.Players.Values)
        {
            bool won = mp.Team == winner;
            double mult = match.Mode == MatchMode.Bots ? ProfileSettings.BotsMultiplier : 1.0;
            int points = (int)Math.Round(((won ? ProfileSettings.WinPoints : ProfileSettings.LossPoints) + mp.Kills * ProfileSettings.PerKill + mp.BotKills) * mult);
            int lp = 0;
            LpBreakdown? breakdown = null;
            if (match.IsRanked)
            {
                double own = LpOf(mp.Team), other = LpOf(mp.Team == Faction.Blue ? Faction.Red : Faction.Blue);
                double expected = 1.0 / (1.0 + Math.Pow(10, (other - own) / 200.0));
                int raw = (int)Math.Round(ProfileSettings.LpK * ((won ? 1.0 : 0.0) - expected));
                lp = won ? Math.Clamp(raw, 6, 40) : Math.Clamp(raw, -40, -6);
                breakdown = new LpBreakdown(own, other, expected, ProfileSettings.LpK, raw, lp, won ? 6 : -40, won ? 40 : -6, lp);
            }
            (int gained, int lpChange) = server.Players.Find(mp.Id) is { } player
                ? profiles.Award(player, won, points, lp, match.IsRanked) : (0, 0);
            if (breakdown is not null) breakdown = breakdown with { Applied = lpChange };
            int total = server.Players.Find(mp.Id) is { } p2 && profiles.Of(p2) is { } profile ? profile.Lp : mp.StartLp;
            match.Results.Add(new MatchResult(mp.Id, mp.Name, mp.Team, mp.Hero, won, mp.Kills, mp.Deaths, mp.BotKills, gained, lpChange, total, mp.Assists, breakdown));
        }
        server.Log($"[MATCH #{match.Id}] koniec: wygrali {(winner == Faction.Blue ? "niebiescy" : "czerwoni")}");
        Finished?.Invoke(match);
        RoomsChanged?.Invoke();
        RoomChanged?.Invoke(match);
    }

    private int PenaltyLp(Match match, MatchPlayer mp) => -(ProfileSettings.LpK / 2 + 4);

    private void ForfeitOffline(Match match, MatchPlayer mp)
    {
    }

    private void ReturnToLobby(Player player, MatchPlayer mp)
    {
        HeroSetup.Clear(player, mp);
        if (player.IsDead) player.Respawn();
        HeroSetup.ResetCharacter(player);
        player.VirtualWorld = VirtualWorldId.Default;
        EnterArenaWorld(player);
        player.MaxHealth = ArenaSettings.PlayerHealth;
        player.Health = ArenaSettings.PlayerHealth;
        player.Position = ArenaSettings.Center;
        lobby.Add(player.Id);
        SetLobbyGhost(player, true);
        Teams.Set(player, Faction.Blue);
        server.Network.Send(player, new Lobby.ArenaPhaseMessage(0, 0));
    }

    private void Dispose(Match match)
    {
        foreach (MatchPlayer mp in match.Players.Values.ToArray())
        {
            byPlayer.Remove(mp.Id);
            if (server.Players.Find(mp.Id) is { IsValid: true } player) ReturnToLobby(player, mp);
        }
        match.Players.Clear();
        match.Director?.Dispose();
        matches.Remove(match.Id);
        RoomsChanged?.Invoke();
    }

    public void Shutdown()
    {
        foreach (Match match in matches.Values.ToArray()) Dispose(match);
    }
}
