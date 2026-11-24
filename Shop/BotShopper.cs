using BotArena.Heroes;
using BotArena.Matches;

namespace BotArena.Shop;

public static class BotShopper
{
    public const float ShopRange = 1800f;

    public static void Start(MatchBot bot, HeroDef hero)
    {
        bot.Build.Clear();
        bot.Build.AddRange(BuildFor(hero));
        if (Starters(hero).FirstOrDefault(i => i.Cost <= bot.Gold) is { } starter) Buy(bot, starter);
    }

    private static List<(ItemDef Item, float Score)> Ranked(HeroDef hero)
    {
        HashSet<string> parts = [.. ItemCatalog.All.SelectMany(i => i.Components)];
        return ItemCatalog.All
            .Where(i => i.Use is not { Kind: not UseKind.Active } && i.Group is not ("basic" or "component" or "starter" or "consumable") && !parts.Contains(i.Key))
            .Select(i => (Item: i, Score: Value(hero.Role, i.Stats)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Item.Cost).ThenBy(x => x.Item.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ItemDef> Starters(HeroDef hero) => ItemCatalog.All
        .Where(i => i.Group == "starter" && Value(hero.Role, i.Stats) > 0)
        .OrderByDescending(i => Value(hero.Role, i.Stats)).ThenBy(i => i.Cost);

    public sealed record AdviceSection(string Id, string Title, string Hint, string[] Items);

    public static (List<string> Build, List<AdviceSection> Sections) Advice(HeroDef hero)
    {
        List<(ItemDef Item, float Score)> ranked = Ranked(hero);
        List<string> build = BuildFor(hero);
        List<string> core = [.. build.Where(k => ItemCatalog.Find(k)?.Group != "boots")];
        string? boots = build.FirstOrDefault(k => ItemCatalog.Find(k)?.Group == "boots");

        var start = Starters(hero).Where(i => i.Cost <= MatchPlayer.StartGold).Take(2).Select(i => i.Key).ToList();
        start.AddRange(ItemCatalog.All.Where(i => i.Group == "consumable").Select(i => i.Key));

        var early = new List<string>();
        foreach (string key in core.Take(1).Concat(boots is null ? Enumerable.Empty<string>() : new[] { boots }))
            if (ItemCatalog.Find(key) is { } item)
                foreach (string part in item.Components) if (!early.Contains(part)) early.Add(part);
        early = [.. early.OrderBy(k => ItemCatalog.Find(k)?.Cost ?? 0)];

        var bootList = new List<string>();
        if (boots is not null) bootList.Add(boots);
        foreach ((ItemDef item, _) in ranked) if (item.Group == "boots" && !bootList.Contains(item.Key) && bootList.Count < 3) bootList.Add(item.Key);

        var situational = new List<string>();
        void AddBest(Func<ItemStats, float> score)
        {
            ItemDef? best = ranked.Select(x => x.Item).Concat(ItemCatalog.All.Where(i => i.Group == "defense"))
                .Where(i => i.Group != "boots" && !build.Contains(i.Key) && !situational.Contains(i.Key)).OrderByDescending(i => score(i.Stats)).FirstOrDefault();
            if (best is not null && score(best.Stats) > 0) situational.Add(best.Key);
        }
        AddBest(s => s.Armor + s.Health * 0.05f);
        AddBest(s => s.Resist + s.Health * 0.05f);
        foreach ((ItemDef item, _) in ranked)
            if (situational.Count < 5 && item.Group != "boots" && !build.Contains(item.Key) && !situational.Contains(item.Key)) situational.Add(item.Key);

        var sections = new List<AdviceSection>
        {
            new("start", "Na start", "Kup od razu w bazie: przedmiot startowy i mikstury zdrowia (klawisze 4–9).", [.. start]),
            new("early", "Wczesne", "Składniki pierwszych przedmiotów — kup, gdy wrócisz do bazy z mniejszą ilością złota.", [.. early]),
            new("core", "Kluczowe", $"Najważniejsze przedmioty dla roli „{hero.Role}”, w tej kolejności.", [.. core.Take(3)]),
            new("late", "Na później", "Domknięcie ekwipunku.", [.. core.Skip(3)]),
            new("situational", "Sytuacyjne", "Gdy wróg mocno bije: pancerz na fizycznych, odporność na magów.", [.. situational]),
        };
        return (build, [.. sections.Where(s => s.Items.Length > 0)]);
    }

    public static List<string> BuildFor(HeroDef hero)
    {
        List<(ItemDef Item, float Score)> finals = Ranked(hero);
        var build = finals.Where(x => x.Item.Group != "boots").Take(ItemCatalog.Slots).Select(x => x.Item.Key).ToList();
        if (finals.FirstOrDefault(x => x.Item.Group == "boots") is { Item: { } boots })
        {
            if (build.Count >= ItemCatalog.Slots) build.RemoveAt(build.Count - 1);
            build.Insert(Math.Min(1, build.Count), boots.Key);
        }
        return build;
    }

    public static float Value(string role, ItemStats s) => role switch
    {
        "Strzelec" => s.Ad + s.Crit * 1.6f + s.CritDmg * 0.8f + s.Ls * 1.5f + s.Leth * 1.2f + s.ArmPen * 1.2f,
        "Zabójca" => s.Ad + s.Leth * 2f + s.Crit * 1f + s.Cdr * 1.2f + s.ArmPen * 1.2f + s.Health * 0.03f,
        "Mag" => s.Ap + s.MagPen * 1.5f + s.MagPenPct * 1.8f + s.Cdr * 1.5f + s.Health * 0.03f,
        "Wsparcie" => s.Ap * 0.8f + s.HealPower * 1.5f + s.Cdr * 2f + s.Health * 0.06f + s.Armor * 0.6f + s.Resist * 0.6f + s.HpRegen * 0.5f,
        "Tank" => s.Health * 0.1f + s.Armor * 1.3f + s.Resist * 1.2f + s.HpRegen * 0.6f + s.Cdr * 0.8f + s.Ten * 0.3f,
        _ => s.Ad + s.Health * 0.07f + s.Armor * 0.6f + s.Resist * 0.5f + s.Cdr * 1f + s.Ov * 1.5f + s.Ls * 1f + s.Leth * 0.8f,
    };

    public static bool Shop(MatchBot bot)
    {
        bool bought = false;
        for (int guard = 0; guard < 16 && NextBuy(bot) is { } item && Buy(bot, item); guard++) bought = true;
        return bought;
    }

    public static bool CanAffordNext(MatchBot bot) =>
        bot.Build.FirstOrDefault(key => !bot.Items.Contains(key)) is { } next && ItemCatalog.Find(next) is { } item && ItemCatalog.PriceFor(bot.Items, item) <= bot.Gold;

    private static ItemDef? NextBuy(MatchBot bot)
    {
        foreach (string key in bot.Build)
        {
            if (bot.Items.Contains(key)) continue;
            return ItemCatalog.Find(key) is { } target ? Affordable(bot, target, 0) : null;
        }
        return null;
    }

    private static ItemDef? Affordable(MatchBot bot, ItemDef target, int depth)
    {
        if (ItemCatalog.PriceFor(bot.Items, target) <= bot.Gold) return target;
        if (depth > 4) return null;
        var missing = target.Components.ToList();
        foreach (string owned in bot.Items) missing.Remove(owned);
        ItemDef? best = null;
        foreach (string key in missing)
            if (ItemCatalog.Find(key) is { } part && Affordable(bot, part, depth + 1) is { } pick && (best is null || pick.Cost > best.Cost)) best = pick;
        return best;
    }

    private static bool Buy(MatchBot bot, ItemDef item)
    {
        (List<int> used, int credit) = ItemCatalog.Credit(bot.Items, item);
        if (Math.Max(0, item.Cost - credit) > bot.Gold) return false;
        if (bot.Items.Count - used.Count + 1 > ItemCatalog.Slots)
        {
            if (!SellFiller(bot, item)) return false;
            (used, credit) = ItemCatalog.Credit(bot.Items, item);
        }
        int price = Math.Max(0, item.Cost - credit);
        if (price > bot.Gold) return false;
        foreach (int index in used.OrderByDescending(i => i)) bot.Items.RemoveAt(index);
        bot.Items.Add(item.Key);
        bot.Gold -= price;
        return true;
    }

    private static bool SellFiller(MatchBot bot, ItemDef buying)
    {
        int slot = bot.Items.FindIndex(key => ItemCatalog.Find(key)?.Group == "starter");
        if (slot < 0) slot = bot.Items.FindIndex(key => !bot.Build.Contains(key) && !Contains(buying, key, 0) &&!IsPartOfBuild(bot, key));
        if (slot < 0) return false;
        bot.Gold += (int)(ItemCatalog.Find(bot.Items[slot])?.Cost * ItemCatalog.SellRatio ?? 0);
        bot.Items.RemoveAt(slot);
        return true;
    }

    private static bool IsPartOfBuild(MatchBot bot, string key)
    {
        foreach (string goal in bot.Build)
            if (!bot.Items.Contains(goal) && ItemCatalog.Find(goal) is { } item && Contains(item, key, 0)) return true;
        return false;
    }

    private static bool Contains(ItemDef item, string key, int depth) =>
        depth < 5 && item.Components.Any(part => part == key || (ItemCatalog.Find(part) is { } sub && Contains(sub, key, depth + 1)));

    public static (HeroStats Stats, int MaxHealth, int Damage) PowerOf(HeroDef hero, MatchBot bot)
    {
        int level = bot.Level;
        HeroStats b = HeroResources.StatsOf(hero, level);
        ItemStats it = ItemCatalog.Bonus(bot.Items);
        var stats = new HeroStats(b.Ad + it.Ad, b.Ap + it.Ap, Math.Min(100, b.Crit + it.Crit), b.Armor + it.Armor, b.Resist + it.Resist,
            Math.Min(HeroResources.MaxCdr, b.Cdr + it.Cdr), b.Regen + it.HpRegen);
        return (stats, HeroResources.MaxHealth(hero, level) + it.Health, HeroResources.BasicAttackOf(hero, stats).Damage);
    }
}
