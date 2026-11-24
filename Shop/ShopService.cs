using BotArena.Heroes;
using BotArena.Lobby;
using BotArena.Matches;

namespace BotArena.Shop;

public sealed class ShopService(Server server, MatchService matches, LobbyService lobby, AbilityService abilities)
{
    public const float BaseRadius = 1800f;

    private readonly HashSet<PlayerId> open = [];
    private sealed record Hot(int PerTick, int Left, long NextAt);
    private readonly Dictionary<PlayerId, Hot> hots = [];
    private readonly Dictionary<(PlayerId, string), long> cooldowns = [];
    private readonly Dictionary<PlayerId, (int Match, bool Inside)> zone = [];

    public void Start() => server.Timers.Every(TimeSpan.FromMilliseconds(500), Tick);

    public bool InBase(Player player)
    {
        if (player.IsDead || matches.Of(player) is not { State: MatchState.Running } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return false;
        Position spawn = LaneSettings.Lanes[0].SpawnOf(mp.Team);
        float dx = player.Position.X - spawn.X, dz = player.Position.Z - spawn.Z;
        return dx * dx + dz * dz <= BaseRadius * BaseRadius;
    }

    private void Tick()
    {
        foreach (Match match in matches.All)
        {
            if (match.State != MatchState.Running) continue;
            foreach (MatchPlayer mp in match.Players.Values)
            {
                if (server.Players.Find(mp.Id) is not { IsValid: true } player) continue;
                Effects(player, match, mp);
                bool inside = InBase(player);
                bool first = !(zone.TryGetValue(mp.Id, out var known) && known.Match == match.Id);
                if (!first && known.Inside == inside) continue;
                zone[mp.Id] = (match.Id, inside);
                lobby.Push(player, "shop.zone", new { inside, catalog = first ? CatalogDto() : null });
                if (!inside && open.Remove(mp.Id)) lobby.Push(player, "shop", new { open = false });
            }
        }
    }

    private void Effects(Player player, Match match, MatchPlayer mp)
    {
        long now = Environment.TickCount64;
        if (player.IsDead) hots.Remove(mp.Id);
        else if (hots.TryGetValue(mp.Id, out Hot? hot) && now >= hot.NextAt)
        {
            player.Health = Math.Min(player.MaxHealth, player.Health + hot.PerTick);
            if (hot.Left <= 1) hots.Remove(mp.Id); else hots[mp.Id] = hot with { Left = hot.Left - 1, NextAt = hot.NextAt + 1000 };
        }
        if (mp.Elixirs.Count == 0) return;
        string[] ended = [.. mp.Elixirs.Where(e => player.IsDead || now >= e.Value).Select(e => e.Key)];
        if (ended.Length == 0) return;
        ItemStats before = ItemCatalog.Bonus(mp);
        foreach (string key in ended) mp.Elixirs.Remove(key);
        Changed(player, match, mp, before);
        lobby.Toast(player, ended.Length == 1 ? $"{(ended[0].StartsWith("buff_") ? "Błogosławieństwo" : "Eliksir")} przestało działać: {ItemCatalog.ElixirName(ended[0])}." : "Eliksiry przestały działać.", "info");
    }

    public void Forget(Player player)
    {
        hots.Remove(player.Id);
        foreach ((PlayerId, string) pause in cooldowns.Keys.Where(k => k.Item1 == player.Id).ToArray()) cooldowns.Remove(pause);
        open.Remove(player.Id);
        zone.Remove(player.Id);
    }

    public void Handle(Player player, string action, string key, int slot)
    {
        if (matches.Of(player) is not { State: MatchState.Running } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return;
        switch (action)
        {
            case "item.close":
                open.Remove(player.Id);
                break;
            case "item.use":
                if (Use(player, match, mp, slot) is { } refused) lobby.Toast(player, refused, "error");
                break;
            case "item.open":
                if (!InBase(player)) { lobby.Toast(player, "Sklep jest dostępny tylko w bazie.", "error"); open.Remove(player.Id); lobby.Push(player, "shop", new { open = false }); return; }
                open.Add(player.Id);
                PushState(player, mp, withCatalog: true);
                break;
            case "item.buy":
            {
                if (!open.Contains(player.Id) || !InBase(player)) { lobby.Toast(player, "Sklep jest dostępny tylko w bazie.", "error"); return; }
                if (Buy(player, match, mp, key) is { } error) lobby.Toast(player, error, "error");
                break;
            }
            case "item.sell":
            {
                if (!open.Contains(player.Id) || !InBase(player)) { lobby.Toast(player, "Sprzedawać można tylko w bazie.", "error"); return; }
                if (Sell(player, match, mp, slot) is { } error) lobby.Toast(player, error, "error");
                break;
            }
            case "item.move":
            {
                if (!open.Contains(player.Id) || !InBase(player)) { lobby.Toast(player, "Przedmioty przekładasz w sklepie, w bazie.", "error"); return; }
                if (int.TryParse(key, out int to) && Move(mp, slot, to) is { } error) lobby.Toast(player, error, "error");
                else Changed(player, match, mp, ItemCatalog.Bonus(mp));
                break;
            }
        }
    }

    private static int Filled(MatchPlayer mp) => mp.Items.Count(k => k.Length > 0);

    private static int FreeSlot(MatchPlayer mp)
    {
        int hole = mp.Items.FindIndex(k => k.Length == 0);
        return hole >= 0 ? hole : mp.Items.Count;
    }

    private static void Trim(MatchPlayer mp)
    {
        while (mp.Items.Count > 0 && mp.Items[^1].Length == 0) mp.Items.RemoveAt(mp.Items.Count - 1);
    }

    private static void PutAt(MatchPlayer mp, int slot, string key)
    {
        while (mp.Items.Count <= slot) mp.Items.Add("");
        mp.Items[slot] = key;
    }

    private static string? Move(MatchPlayer mp, int from, int to)
    {
        if (from < 0 || from >= mp.Items.Count || mp.Items[from].Length == 0) return "Pusty slot.";
        if (to < 0 || to >= ItemCatalog.Slots) return "Nie ma takiego slotu.";
        if (to == from) return null;
        string moved = mp.Items[from];
        string other = to < mp.Items.Count ? mp.Items[to] : "";
        PutAt(mp, to, moved);
        mp.Items[from] = other;
        Trim(mp);
        return null;
    }

    private string? Buy(Player player, Match match, MatchPlayer mp, string key)
    {
        if (ItemCatalog.Find(key) is not { } item) return "Nie ma takiego przedmiotu.";
        if (item.Consumable) return BuyUsable(player, match, mp, item);
        (List<int> used, int credit) = ItemCatalog.Credit(mp.Items, item);
        int price = Math.Max(0, item.Cost - credit);
        if (Filled(mp) - used.Count + 1 > ItemCatalog.Slots) return "Brak miejsca w ekwipunku (6 przedmiotów) — sprzedaj coś.";
        if (mp.Gold < price) return $"Za mało złota: {item.Name} kosztuje {price}.";
        ItemStats before = ItemCatalog.Bonus(mp);
        int target = used.Count > 0 ? used.Min() : -1;
        foreach (int index in used) mp.Items[index] = "";
        PutAt(mp, target >= 0 ? target : FreeSlot(mp), item.Key);
        Trim(mp);
        mp.Gold -= price;
        Changed(player, match, mp, before);
        lobby.Toast(player, $"Kupiono: {item.Name} (−{price} złota).", "ok");
        return null;
    }

    private string? BuyUsable(Player player, Match match, MatchPlayer mp, ItemDef item)
    {
        int max = item.Use!.MaxCharges, have = mp.Items.Contains(item.Key) ? mp.Charges.GetValueOrDefault(item.Key, 1) : 0;
        if (have >= max) return $"Masz już maksimum ({max}): {item.Name}.";
        if (have == 0 && Filled(mp) + 1 > ItemCatalog.Slots) return "Brak miejsca w ekwipunku (6 przedmiotów) — sprzedaj coś.";
        if (mp.Gold < item.Cost) return $"Za mało złota: {item.Name} kosztuje {item.Cost}.";
        ItemStats before = ItemCatalog.Bonus(mp);
        if (have == 0) PutAt(mp, FreeSlot(mp), item.Key);
        mp.Charges[item.Key] = have + 1;
        mp.Gold -= item.Cost;
        Changed(player, match, mp, before);
        lobby.Toast(player, $"Kupiono: {item.Name} (−{item.Cost} złota).", "ok");
        return null;
    }

    private string? Use(Player player, Match match, MatchPlayer mp, int slot)
    {
        if (player.IsDead || slot < 0 || slot >= mp.Items.Count) return null;
        if (abilities.InStasis(player)) return "W stasis nic nie zrobisz.";
        if (ItemCatalog.Find(mp.Items[slot]) is not { Use: { } use } item) return null;
        long now = Environment.TickCount64;
        if (cooldowns.TryGetValue((mp.Id, item.Key), out long until) && now < until) return null;
        ItemStats before = ItemCatalog.Bonus(mp);
        switch (use.Kind)
        {
            case UseKind.Heal:
                if (hots.ContainsKey(mp.Id)) return "Mikstura już działa.";
                if (player.Health >= player.MaxHealth) return "Masz pełne zdrowie.";
                int ticks = Math.Max(1, (int)MathF.Round(use.Seconds));
                hots[mp.Id] = new Hot((int)MathF.Ceiling(use.Amount / (float)ticks), ticks, now + 1000);
                break;
            case UseKind.Mana:
                if (!abilities.GiveMana(player, use.Amount)) return "Ten bohater nie ma many.";
                break;
            case UseKind.Elixir:
                if (mp.Elixirs.ContainsKey(item.Key)) return "Ten eliksir już działa.";
                mp.Elixirs[item.Key] = now + (long)(use.Seconds * 1000);
                break;
            case UseKind.Active:
                if (abilities.UseItem(player, use, lobby.FacingOf(player)) is { } denied) return denied;
                break;
        }
        if (item.Consumable)
        {
            int charges = mp.Charges.GetValueOrDefault(item.Key, 1) - 1;
            if (charges <= 0) { mp.Items[slot] = ""; Trim(mp); mp.Charges.Remove(item.Key); } else mp.Charges[item.Key] = charges;
        }
        cooldowns[(mp.Id, item.Key)] = now + (long)(use.Cooldown * 1000);
        Changed(player, match, mp, before);
        lobby.Push(player, "item.used", new { key = item.Key, ms = (int)(use.Cooldown * 1000) });
        return null;
    }

    private string? Sell(Player player, Match match, MatchPlayer mp, int slot)
    {
        if (slot < 0 || slot >= mp.Items.Count || mp.Items[slot].Length == 0) return "Pusty slot.";
        ItemDef? item = ItemCatalog.Find(mp.Items[slot]);
        int refund = item is null ? 0 : (int)Math.Round(item.Cost * ItemCatalog.SellRatio) * (item.Consumable ? mp.Charges.GetValueOrDefault(item.Key, 1) : 1);
        ItemStats before = ItemCatalog.Bonus(mp);
        if (item is { Consumable: true }) mp.Charges.Remove(item.Key);
        mp.Items[slot] = "";
        Trim(mp);
        mp.Gold += refund;
        Changed(player, match, mp, before);
        lobby.Toast(player, $"Sprzedano: {item?.Name} (+{refund} złota).", "ok");
        return null;
    }

    private void Changed(Player player, Match match, MatchPlayer mp, ItemStats before)
    {
        abilities.ApplyItems(player, before, ItemCatalog.Bonus(mp));
        matches.RefreshStats(match, mp);
        if (open.Contains(player.Id)) PushState(player, mp, withCatalog: false);
    }

    private static object[] CatalogDto() =>
        [.. ItemCatalog.All.Select(i => (object)new
        {
            key = i.Key, name = i.Name, group = i.Group, cost = i.Cost, glyph = i.Glyph, shape = i.Shape, desc = i.Desc, builds = i.Components,
            use = i.Use is null ? null : new { kind = i.Use.Kind.ToString().ToLowerInvariant(), amount = i.Use.Amount, seconds = i.Use.Seconds, max = i.Use.MaxCharges, boost = StatsDto(i.Use.Boost), active = i.Use.Active, radius = i.Use.Radius, cooldown = i.Use.Cooldown },
            stats = StatsDto(i.Stats),
        })];

    private static object StatsDto(ItemStats s) => new
    {
        ad = s.Ad, ap = s.Ap, armor = s.Armor, resist = s.Resist, health = s.Health, mana = s.Mana, crit = s.Crit, cdr = s.Cdr, hpRegen = s.HpRegen,
        @as = s.As, ms = s.Ms, ls = s.Ls, ov = s.Ov, leth = s.Leth, armPen = s.ArmPen, magPen = s.MagPen, magPenPct = s.MagPenPct, ten = s.Ten, healPower = s.HealPower, manaRegen = s.ManaRegen, critDmg = s.CritDmg,
    };

    private void PushState(Player player, MatchPlayer mp, bool withCatalog)
    {
        object[]? catalog = withCatalog ? CatalogDto() : null;
        lobby.Push(player, "shop", new
        {
            open = true, gold = mp.Gold, owned = mp.Items.ToArray(), charges = mp.Items.Select(k => mp.Charges.GetValueOrDefault(k, 1)).ToArray(), slots = ItemCatalog.Slots, sellRatio = ItemCatalog.SellRatio,
            prices = ItemCatalog.All.Select(i => ItemCatalog.PriceFor(mp.Items, i)).ToArray(), catalog, advice = AdviceDto(mp),
        });
    }

    private static object? AdviceDto(MatchPlayer mp)
    {
        if (Heroes.HeroCatalog.Find(mp.Hero) is not { } hero) return null;
        (List<string> build, List<BotShopper.AdviceSection> sections) = BotShopper.Advice(hero);
        return new
        {
            hero = hero.Name, role = hero.Role, build,
            sections = sections.Select(s => new { id = s.Id, title = s.Title, hint = s.Hint, items = s.Items }).ToArray(),
        };
    }
}
