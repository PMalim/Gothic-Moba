using GothicOnline.Api;
using BotArena.Heroes;

namespace BotArena.Matches;

public static class HeroSetup
{
    public const int Arrows = 300;

    public static readonly Dictionary<PlayerId, PlayerVisual> MorphOwnLook = [];

    public static void Apply(Player player, HeroDef hero, MatchPlayer mp)
    {
        Clear(player, mp, restoreLook: false);
        ApplyBody(player, hero);
        ApplyLook(player, hero, mp);
        int maxHealth = HeroResources.MaxHealth(hero, mp.Level) + Shop.ItemCatalog.Bonus(mp).Health;
        player.MaxHealth = maxHealth;
        player.Health = maxHealth;
        player.Strength = hero.Strength;
        player.Dexterity = hero.Dexterity;
        player.SetWeaponSkill(WeaponSkill.OneHanded, new Proficiency(hero.WeaponClass == HeroWeapon.OneHanded ? 80 : 30));
        player.SetWeaponSkill(WeaponSkill.TwoHanded, new Proficiency(hero.WeaponClass == HeroWeapon.TwoHanded ? 80 : 30));
        player.SetWeaponSkill(WeaponSkill.Bow, new Proficiency(hero.WeaponClass == HeroWeapon.Bow ? 80 : 30));
        player.SetRespawnDelay(ArenaSettings.PlayerRespawnDelay);
        if (hero.WeaponMana is { } weaponMana && hero.Rune is null)
        {
            try { mp.OwnMagic ??= (player.MagicLevel, player.MaxMana); player.MaxMana = weaponMana; player.Mana = weaponMana; }
            catch (Exception) {  }
        }
        if (hero.Weapon is { } weapon) Give(player, mp, weapon, 1);
        if (hero.Armor is { } armor) Give(player, mp, armor, 1);
        if (hero.Ammo is { } ammo) Give(player, mp, ammo, hero.AmmoCount);
        if (hero.Weapon is { } melee) player.Inventory.Equip(melee);
        if (hero.Armor is { } worn) player.Inventory.Equip(worn);
        if (hero.Rune is { } rune) ApplyRune(player, hero, mp, rune);
        mp.Hero = hero.Key;
        foreach (string overlay in hero.Overlays)
        {
            try
            {
                bool applied = player.ApplyOverlay(new MdsName(overlay));
                if (applied) mp.Overlays.Add(overlay);
                Console.WriteLine($"[HERO] {player.Name}: nakladka {overlay} -> {applied}");
            }
            catch (Exception error) { Console.WriteLine($"[HERO] {player.Name}: nakladka {overlay} blad: {error.Message}"); }
        }
    }

    private static void ApplyBody(Player player, HeroDef hero)
    {
        try
        {
            NpcInstance wanted = hero.Instance ?? NpcInstance.Hero;
            if (player.Instance == wanted) return;
            if (hero.Instance is not null && !MorphOwnLook.ContainsKey(player.Id)) MorphOwnLook[player.Id] = player.Visual;
            SheatheWeapon(player);
            player.Instance = wanted;
        }
        catch (Exception error) { Console.WriteLine($"[HERO] {player.Name}: ciało {hero.Instance?.Value ?? "PC_HERO"}: {error.Message}"); }
    }

    private static void ApplyRune(Player player, HeroDef hero, MatchPlayer mp, ItemInstance rune)
    {
        try
        {
            mp.OwnMagic ??= (player.MagicLevel, player.MaxMana);
            player.MagicLevel = hero.MagicCircle;
            player.MaxMana = hero.GameMana;
            player.Mana = hero.GameMana;
            Give(player, mp, rune, 1);
            player.Inventory.Equip(rune, LaneSettings.SpellSlot);
        }
        catch (Exception)
        {
        }
    }

    private static void RestoreMagic(Player player, MatchPlayer mp)
    {
        if (mp.OwnMagic is not { } own) return;
        mp.OwnMagic = null;
        try
        {
            player.MaxMana = own.MaxMana;
            player.Mana = Math.Min(player.Mana, own.MaxMana);
            player.MagicLevel = own.MagicLevel;
        }
        catch (Exception) {  }
    }

    private static void Give(Player player, MatchPlayer mp, ItemInstance item, int amount)
    {
        player.Inventory.Give(item, amount);
        mp.Gear.Add((item, amount));
    }

    private static void ApplyLook(Player player, HeroDef hero, MatchPlayer mp)
    {
        if (hero.Instance is not null) return;
        try
        {
            if (hero.Look is { } look)
            {
                mp.OwnLook ??= player.Visual;
                player.SetVisual(look, hero.Armor);
            }
            else RestoreLook(player, mp);
        }
        catch (Exception)
        {
        }
    }

    private static void RestoreLook(Player player, MatchPlayer mp)
    {
        if (mp.OwnLook is not { } own) return;
        mp.OwnLook = null;
        try { player.SetVisual(own); } catch (Exception) {  }
    }

    public static void SheatheWeapon(Player player)
    {
        try { player.UnreadySpell(); } catch (Exception) {  }
        try { player.RemoveWeapon(); } catch (Exception) {  }
        try { player.WeaponMode = WeaponMode.None; } catch (Exception) {  }
    }

    public static void ResetCharacter(Player player)
    {
        if (!player.IsValid) return;
        SheatheWeapon(player);
        try
        {
            Equipment worn = player.Equipment;
            List<ItemInstance> items = [];
            foreach (ItemInstance? item in new[] { worn.Armor, worn.MeleeWeapon, worn.RangedWeapon, worn.Helmet, worn.Shield, worn.Amulet, worn.Belt,
                         worn.GetRing(Hand.Left), worn.GetRing(Hand.Right) })
                if (item is { } found) items.Add(found);
            for (int slot = 0; slot <= 6; slot++) if (worn.GetSpell(slot) is { } rune) items.Add(rune);
            foreach (ItemInstance item in items.Distinct())
            {
                try { player.Inventory.Unequip(item); } catch (Exception) {  }
                try { player.Inventory.Remove(item, 1); } catch (Exception) {  }
            }
        }
        catch (Exception error) { Console.WriteLine($"[HERO] {player.Name}: reset ekwipunku: {error.Message}"); }
        try
        {
            foreach (MdsName overlay in player.Overlays.ToArray())
            {
                try { player.RemoveOverlay(overlay); } catch (Exception) {  }
            }
        }
        catch (Exception) {  }
        PlayerVisual? look = null;
        if (MorphOwnLook.Remove(player.Id, out PlayerVisual? morphed)) look = morphed;
        else
        {
            try { look = player.Visual; } catch (Exception) {  }
        }
        try { player.Instance = NpcInstance.Hero; }
        catch (Exception error) { Console.WriteLine($"[HERO] {player.Name}: reset instancji: {error.Message}"); }
        if (look is not null)
        {
            try { player.SetVisual(look); } catch (Exception) {  }
        }
        SheatheWeapon(player);
    }

    public static void Clear(Player player, MatchPlayer mp, bool restoreLook = true)
    {
        SheatheWeapon(player);
        if (restoreLook) RestoreLook(player, mp);
        foreach (string overlay in mp.Overlays)
        {
            try { player.RemoveOverlay(new MdsName(overlay)); } catch (Exception) {  }
        }
        mp.Overlays.Clear();
        RestoreMagic(player, mp);
        foreach ((ItemInstance item, int amount) in mp.Gear)
        {
            try
            {
                player.Inventory.Unequip(item);
                player.Inventory.Remove(item, amount);
            }
            catch (Exception)
            {
            }
        }
        mp.Gear.Clear();
    }
}
