using BotArena.GameData;
using BotArena.Matches;
using BotArena.Shop;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{
    private const string FxStasis = "spellFX_ItemStasis", FxStasisFlash = "spellFX_ItemStasisFlash", FxCleanse = "spellFX_ItemCleanse", FxQuicksilver = "spellFX_ItemQuicksilver",
        FxAegis = "spellFX_ItemAegis", FxAegisRing = "spellFX_ItemAegisRing", FxBless = "spellFX_ItemBless", FxMercyGround = "spellFX_ItemMercyGround", FxMercyHeal = "spellFX_ItemMercyHeal",
        FxSong = "spellFX_ItemSong", FxSongRing = "spellFX_ItemSongRing", FxWhirl = "spellFX_ItemWhirl", FxWhirlHit = "spellFX_ItemWhirlHit",
        FxLeapTrail = "spellFX_ItemLeapTrail", FxLeapLand = "spellFX_ItemLeapLand", FxChainsGround = "spellFX_ItemChainsGround", FxChainsHit = "spellFX_ItemChainsHit";

    private const float LeapDistance = 600f, LeapImpact = 0.7f;
    private const int LeapMillis = 800, WhirlMillis = 900;
    private const float MercyDelay = 1.4f;

    private sealed record Later(TimeSpan Due, Action Run);
    private readonly List<Later> later = [];
    private readonly Dictionary<PlayerId, TimeSpan> stasis = [];

    private void After(double seconds, Action run) => later.Add(new Later(server.Clock.Elapsed + TimeSpan.FromSeconds(seconds), run));

    private void RunLater(TimeSpan now)
    {
        if (later.Count == 0) return;
        foreach (Later item in later.Where(l => l.Due <= now).ToArray())
        {
            later.Remove(item);
            try { item.Run(); }
            catch (Exception e) { server.Log($"[ITEM] opozniony skutek: {e.Message}"); }
        }
    }

    public string? UseItem(Player player, ItemUse use, (float X, float Z) facing)
    {
        if (player.IsDead || matches.Of(player) is not { State: MatchState.Running, Director: { } director } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return "Nie teraz.";
        TimeSpan now = server.Clock.Elapsed;
        if (InStasis(player)) return "W stasis nic nie zrobisz.";
        return use.Active switch
        {
            "stasis" => ItemStasis(match, player, use, now),
            "cleanse" => ItemCleanse(match, player, use, now, immunity: false),
            "quicksilver" => ItemCleanse(match, player, use, now, immunity: true),
            "aegis" => ItemAegis(match, mp, player, use, now),
            "blessing" => ItemBlessing(match, mp, player, use, now),
            "mercy" => ItemMercy(match, mp, player, use),
            "song" => ItemSong(match, mp, player, use),
            "whirl" => ItemWhirl(match, director, mp, player, use),
            "leap" => ItemLeap(match, director, mp, player, use, facing),
            "chains" => ItemChains(match, director, mp, player, use, now),
            _ => "Ten przedmiot nie ma aktywacji.",
        };
    }

    public bool InStasis(Player player) => stasis.TryGetValue(player.Id, out TimeSpan until) && server.Clock.Elapsed < until;

    private List<Player> AlliesAround(Match match, Faction team, Player caster, Position at, float radius)
    {
        var list = new List<Player> { caster };
        foreach (MatchPlayer other in match.Players.Values.Where(p => p.Team == team && p.Id != caster.Id))
            if (server.Players.Find(other.Id) is { IsValid: true, IsDead: false } friend && Distance(at, friend.Position) <= radius) list.Add(friend);
        return list;
    }

    private void GroundFx(Match match, string fx, Position at, float seconds, int caster = -1) =>
        matches.Send(match, new AbilityGroundMessage(caster, fx, at, (int)(seconds * 1000)));

    private string? ItemStasis(Match match, Player player, ItemUse use, TimeSpan now)
    {
        TimeSpan until = now + TimeSpan.FromSeconds(use.Seconds);
        stasis[player.Id] = until;
        pending.RemoveAll(p => p.Unfreeze && ReferenceEquals(p.Target, player));
        drags.RemoveAll(d => ReferenceEquals(d.Who, player));
        shields[player.Id] = (until, 1f);
        server.Network.Send(player, new ArenaFreezeMessage(true));
        pending.Add(new Pending(until, player, 0, null, Unfreeze: true));
        Fx(match, player, null, 1, FxStasisFlash);
        Aura(match, player, FxStasis, use.Seconds);
        matches.Send(match, new ArenaStasisMessage(MobCommands.IdOf(player), true));
        After(use.Seconds, () =>
        {
            if (!player.IsValid) return;
            Fx(match, player, null, 1, FxStasisFlash);
            matches.Send(match, new ArenaStasisMessage(MobCommands.IdOf(player), false));
        });
        return null;
    }

    private string? ItemCleanse(Match match, Player player, ItemUse use, TimeSpan now, bool immunity)
    {
        if (InStasis(player)) return "W stasis nic cię nie dotknie.";
        Cleanse(player);
        if (immunity && use.Seconds > 0)
        {
            BuffState state = Buffs(player);
            state.IronUntil = TimeSpan.FromTicks(Math.Max(state.IronUntil.Ticks, (now + TimeSpan.FromSeconds(use.Seconds)).Ticks));
            Aura(match, player, FxQuicksilver, use.Seconds);
        }
        else Fx(match, player, null, 1, FxCleanse);
        return null;
    }

    private void Cleanse(Player target)
    {
        if (!InStasis(target) && pending.RemoveAll(p => p.Unfreeze && ReferenceEquals(p.Target, target)) > 0)
            server.Network.Send(target, new ArenaFreezeMessage(false));
        bleeds.RemoveAll(b => ReferenceEquals(b.Victim, target));
        if (buffs.TryGetValue(target.Id, out BuffState? state) && state.BleedStacks > 0)
        {
            state.BleedStacks = 0;
            state.BleedUntil = default;
            PushBuffs(target);
        }
    }

    private string? ItemAegis(Match match, MatchPlayer mp, Player player, ItemUse use, TimeSpan now)
    {
        float reduction = Math.Clamp(use.Amount / 100f, 0f, 0.9f);
        foreach (Player ally in AlliesAround(match, mp.Team, player, player.Position, use.Radius))
        {
            if (shields.TryGetValue(ally.Id, out var current) && now < current.Until && current.Reduction >= reduction) continue;
            shields[ally.Id] = (now + TimeSpan.FromSeconds(use.Seconds), reduction);
            Aura(match, ally, FxAegis, use.Seconds);
        }
        GroundFx(match, FxAegisRing, player.Position, 1.3f);
        return null;
    }

    private string? ItemBlessing(Match match, MatchPlayer mp, Player player, ItemUse use, TimeSpan now)
    {
        Player target = AlliesAround(match, mp.Team, player, player.Position, use.Radius).Skip(1).OrderBy(a => Distance(player.Position, a.Position)).FirstOrDefault() ?? player;
        Cleanse(target);
        int healed = (int)Math.Round(use.Amount * (1 + ItemsOf(player).HealPower / 100.0));
        target.Health = Math.Min(target.MaxHealth, target.Health + healed);
        Fx(match, player, target, 2, FxBless);
        if (!ReferenceEquals(target, player)) Notice?.Invoke(player, $"Błogosławieństwo: {target.Name}.");
        return null;
    }

    private string? ItemMercy(Match match, MatchPlayer mp, Player player, ItemUse use)
    {
        Position center = player.Position;
        GroundFx(match, FxMercyGround, center, 1.9f, MobCommands.IdOf(player));
        int amount = (int)Math.Round(use.Amount * (1 + ItemsOf(player).HealPower / 100.0));
        Faction team = mp.Team;
        After(MercyDelay, () =>
        {
            foreach (MatchPlayer other in match.Players.Values.Where(p => p.Team == team))
            {
                if (server.Players.Find(other.Id) is not { IsValid: true, IsDead: false } friend || Distance(center, friend.Position) > use.Radius) continue;
                friend.Health = Math.Min(friend.MaxHealth, friend.Health + amount);
                Fx(match, friend, null, 1, FxMercyHeal);
            }
        });
        return null;
    }

    private string? ItemSong(Match match, MatchPlayer mp, Player player, ItemUse use)
    {
        int millis = (int)(use.Seconds * 1000);
        foreach (Player ally in AlliesAround(match, mp.Team, player, player.Position, use.Radius))
        {
            server.Network.Send(ally, new AbilitySpeedMessage(1f + use.Amount / 100f, millis));
            Aura(match, ally, FxSong, use.Seconds);
        }
        GroundFx(match, FxSongRing, player.Position, 1.3f);
        return null;
    }

    private string? ItemWhirl(Match match, MobDirector director, MatchPlayer mp, Player player, ItemUse use)
    {
        Aura(match, player, FxWhirl, 1.0f);
        Faction team = mp.Team;
        After(WhirlMillis * 0.5 / 1000.0, () =>
        {
            if (!player.IsValid || player.IsDead) return;
            Position at = player.Position;
            TimeSpan due = server.Clock.Elapsed;
            foreach (Target t in Enemies(match, director, team).Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= use.Radius).ToArray())
            {
                Hurt(director, player, t, use.Amount, due);
                Fx(match, player, t.Character, 2, FxWhirlHit);
            }
        });
        Touch(player, server.Clock.Elapsed);
        return null;
    }

    private string? ItemLeap(Match match, MobDirector director, MatchPlayer mp, Player player, ItemUse use, (float X, float Z) facing)
    {
        if (InStasis(player)) return "W stasis nie skoczysz.";
        float length = MathF.Sqrt(facing.X * facing.X + facing.Z * facing.Z);
        if (length < 0.001f || !float.IsFinite(length)) return "Nie można ustalić kierunku skoku.";
        Position from = player.Position;
        var land = new Position(from.X + facing.X / length * LeapDistance, from.Y, from.Z + facing.Z / length * LeapDistance);
        server.Network.Send(player, new AbilityMotionMessage(HeroBuffs.ExecuteAnimation, LeapMillis, 1, land));
        Aura(match, player, FxLeapTrail, 1.0f);
        Faction team = mp.Team;
        After(LeapMillis * LeapImpact / 1000.0, () =>
        {
            Position center = player.IsValid && !player.IsDead ? player.Position : land;
            GroundFx(match, FxLeapLand, center, 1.6f);
            if (!player.IsValid) return;
            TimeSpan due = server.Clock.Elapsed;
            foreach (Target t in Enemies(match, director, team).Where(t => t.Mob is not { Kind.Immobile: true } && Distance(center, t.At) <= use.Radius).ToArray())
                Hurt(director, player, t, use.Amount, due);
        });
        Touch(player, server.Clock.Elapsed);
        return null;
    }

    private string? ItemChains(Match match, MobDirector director, MatchPlayer mp, Player player, ItemUse use, TimeSpan now)
    {
        Position at = player.Position;
        GroundFx(match, FxChainsGround, at, 1.3f);
        foreach (Target t in Enemies(match, director, mp.Team).Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= use.Radius).ToArray())
        {
            Hurt(director, player, t, use.Amount, now + TimeSpan.FromMilliseconds(250));
            if (use.Seconds > 0) Stun(director, t, now + TimeSpan.FromSeconds(use.Seconds));
            Fx(match, player, t.Character, 2, FxChainsHit);
        }
        Touch(player, now);
        return null;
    }
}
