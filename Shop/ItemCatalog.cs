namespace BotArena.Shop;

public readonly record struct ItemStats(int Ad = 0, int Ap = 0, int Armor = 0, int Resist = 0, int Health = 0, int Mana = 0, int Crit = 0, int Cdr = 0, int HpRegen = 0,
    int As = 0, int Ms = 0, int Ls = 0, int Ov = 0, int Leth = 0, int ArmPen = 0, int MagPen = 0, int MagPenPct = 0, int Ten = 0, int HealPower = 0, int ManaRegen = 0, int CritDmg = 0)
{
    public static ItemStats operator +(ItemStats a, ItemStats b) => new(a.Ad + b.Ad, a.Ap + b.Ap, a.Armor + b.Armor, a.Resist + b.Resist, a.Health + b.Health,
        a.Mana + b.Mana, a.Crit + b.Crit, a.Cdr + b.Cdr, a.HpRegen + b.HpRegen,
        a.As + b.As, a.Ms + b.Ms, a.Ls + b.Ls, a.Ov + b.Ov, a.Leth + b.Leth, a.ArmPen + b.ArmPen, a.MagPen + b.MagPen, a.MagPenPct + b.MagPenPct,
        a.Ten + b.Ten, a.HealPower + b.HealPower, a.ManaRegen + b.ManaRegen, a.CritDmg + b.CritDmg);
}

public sealed record ItemDef(string Key, string Name, string Group, int Cost, ItemStats Stats, string Glyph, string[]? Builds = null, ItemUse? Use = null, string Shape = "", string Desc = "")
{
    public string[] Components => Builds ?? [];
    public bool Usable => Use is not null;
    public bool Consumable => Use is { Kind: not UseKind.Active };
}

public enum UseKind { Heal, Mana, Elixir, Active }

public sealed record ItemUse(UseKind Kind, int Amount, float Seconds, float Cooldown, int MaxCharges, ItemStats Boost = default, string Active = "", float Radius = 0);

public static class ItemCatalog
{
    public const int Slots = 6;
    public const float SellRatio = 0.7f;

    public static readonly IReadOnlyList<ItemDef> All =
    [
        .. ItemData.Gear,

        new("mik_zdrowie", "Mikstura Zdrowia", "consumable", 50, default, "♥", Use: new(UseKind.Heal, 150, 12, 1, 5), Shape: "healing-elixir", Desc: "Leczy 150 zdrowia w ciągu 12 sekund; do 5 sztuk w jednym slocie."),
    ];

    private static readonly Dictionary<string, ItemDef> byKey = All.ToDictionary(i => i.Key);

    public static ItemDef? Find(string key) => byKey.GetValueOrDefault(key);

    public static ItemStats Bonus(BotArena.Matches.MatchPlayer mp)
    {
        ItemStats total = Bonus(mp.Items);
        foreach (string key in mp.Elixirs.Keys) if (Find(key)?.Use is { } use) total += use.Boost;
        if (mp.Elixirs.ContainsKey(JungleSettings.BlueKey)) total += new ItemStats(ManaRegen: JungleSettings.BlueManaRegen);
        if (mp.Elixirs.ContainsKey(BaronSettings.BuffKey)) total += new ItemStats(Ad: mp.BaronAd, Ap: mp.BaronAp);
        return total;
    }

    public static string ElixirName(string key) => key switch
    {
        JungleSettings.BlueKey => "Błogosławieństwo Lodowego Golema",
        JungleSettings.RedKey => "Błogosławieństwo Cieniostwora",
        BaronSettings.BuffKey => "Ręka Barona",
        _ => Find(key)?.Name ?? key,
    };

    public static ItemStats Bonus(IEnumerable<string> items)
    {
        ItemStats total = default;
        foreach (string key in items) if (Find(key) is { } item) total += item.Stats;
        return total;
    }

    public static (List<int> Used, int Credit) Credit(IReadOnlyList<string> owned, ItemDef item)
    {
        var free = new List<int>();
        for (int i = 0; i < owned.Count; i++) if (owned[i].Length > 0) free.Add(i);
        var used = new List<int>();
        int credit = 0;
        void Walk(ItemDef node, int depth)
        {
            var missing = new List<ItemDef>();
            foreach (string part in node.Components)
            {
                int at = free.FindIndex(i => owned[i] == part);
                if (at >= 0)
                {
                    used.Add(free[at]);
                    credit += Find(part)?.Cost ?? 0;
                    free.RemoveAt(at);
                }
                else if (Find(part) is { } sub) missing.Add(sub);
            }
            if (depth < 5) foreach (ItemDef sub in missing) Walk(sub, depth + 1);
        }
        Walk(item, 0);
        return (used, credit);
    }

    public static int PriceFor(IReadOnlyList<string> owned, ItemDef item) => Math.Max(0, item.Cost - Credit(owned, item).Credit);
}
