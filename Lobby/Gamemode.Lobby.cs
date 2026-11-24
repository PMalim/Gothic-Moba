using GothicOnline.Commands;
using BotArena.Auth;
using BotArena.Heroes;
using BotArena.Lobby;
using BotArena.Matches;
using BotArena.Profiles;

namespace BotArena;

public sealed partial class Gamemode
{
    private ProfileService profiles = null!;
    private MatchService matches = null!;
    private LobbyService lobby = null!;
    private AbilityService abilities = null!;
    private AuthService auth = null!;
    private Shop.ShopService shop = null!;
    private Nav.NavService nav = null!;
    private Waypoints.WaypointService waypoints = null!;
    private MapEdit.MapObjectService mapObjects = null!;
    private Feedback.FeedbackService feedback = null!;

    private void StartLobby()
    {
        profiles = new ProfileService(server, storage);
        matches = new MatchService(server, profiles, debugFeed);
        matches.SetLobbyGhost = admin.SetHidden;
        matches.BotKilled += player => kills[player.Id] = kills.GetValueOrDefault(player.Id) + 1;
        matches.Start();
        lobby = new LobbyService(server, profiles, matches);
        lobby.Start();
        auth = new AuthService(server, storage, profiles, matches, lobby);
        lobby.Auth = auth;
        abilities = new AbilityService(server, matches);
        abilities.Start();
        Teams.Untargetable = who => who is Player p && abilities.InStasis(p);
        matches.RecentAttacker = abilities.RecentAttacker;
        matches.HeroApplied += (player, hero) => { abilities.Reset(player); lobby.PushHud(player, hero); };
        matches.StatsOf = abilities.StatsOf;
        matches.LevelUp += abilities.ApplyLevel;
        lobby.StatsOf = abilities.StatsOf;
        abilities.Cooldown += lobby.PushCooldown;
        abilities.ManaChanged += lobby.PushMana;
        abilities.BuffsChanged += (player, data) => lobby.Push(player, "buffs", data);
        matches.PlayerHit = abilities.ModifyPlayerHit;
        matches.BasicDamage = abilities.BasicDamage;
        matches.CritMultiplier = abilities.CritFactor;
        matches.BasicMagic = abilities.BasicIsMagic;
        matches.HeroFreeze = abilities.Freeze;
        matches.HeroSlow = abilities.SlowHero;
        matches.DragTo = abilities.DragTo;
        matches.ShreddedMitigation = (player, magic, shred) => abilities.StatsOf(player) is { } own
            ? HeroResources.Mitigation(Math.Max(0, (magic ? own.Resist : own.Armor) - shred)) : 1f;
        matches.PlayerShield = abilities.ShieldFactor;
        lobby.BasicOf = player => abilities.BasicOf(player) is { } b ? (b.Damage, b.Magic, (int)Math.Round(abilities.CritFactor(player) * 100)) : null;
        abilities.Notice += (player, text) => lobby.Toast(player, text, "info");
        shop = new Shop.ShopService(server, matches, lobby, abilities);
        shop.Start();
        lobby.Shop = shop.Handle;
        matches.Rejoined += shop.Forget;
        nav = new Nav.NavService(server, lobby, admin.IsAdmin);
        nav.Load();
        nav.Start();
        waypoints = new Waypoints.WaypointService(server, nav, lobby);
        server.Network.Register<Waypoints.ArenaWaypointsMessage>();
        waypoints.Load();
        nav.Waypoints = waypoints;
        mapObjects = new MapEdit.MapObjectService(nav, lobby, matches);
        mapObjects.Load();
        nav.MapObjects = mapObjects;
        MobCommands.Navigator = (from, to) => waypoints.Route(from, to) ?? nav.Route(from, to);
        lobby.Nav = nav.Handle;
        feedback = new Feedback.FeedbackService(server, storage, profiles, matches, lobby);
        lobby.Feedback = feedback.Handle;
        lobby.SkillUp = abilities.RankUp;
        admin.MinionCount = () => matches.All.Sum(m => m.Director?.Mobs.Count(mob => mob.Lane is not null && !mob.Kind.Immobile && mob.Npc is not null) ?? 0);
    }

    [Command("gry", Description = "lista trwających gier", Aliases = ["games"])]
    private string Games(Player player)
    {
        Match[] all = [.. matches.All];
        return all.Length == 0 ? "Nie trwa żadna gra." : string.Join("; ", all.Select(m => $"#{m.Id} {m.Title} ({m.State}, {m.Players.Count}/{m.MaxPlayers})"));
    }

    [Command("lobby", Description = "wyjście z gry do lobby", Aliases = ["wyjdz"])]
    private string LeaveToLobby(Player player)
    {
        if (matches.Of(player) is null) return "Jesteś w lobby.";
        matches.Leave(player);
        return "Wróciłeś do lobby.";
    }
}
