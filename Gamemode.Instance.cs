using System.Reflection;
using BotArena.GameData;
using BotArena.Heroes;
using BotArena.Matches;
using GothicOnline.Api;
using GothicOnline.Commands;

namespace BotArena;

public sealed partial class Gamemode
{
    private static readonly Lazy<Dictionary<string, NpcInstance>> GameInstances = new(() => typeof(GameNpcs)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(NpcInstance))
        .ToDictionary(field => field.Name, field => (NpcInstance)field.GetValue(null)!, StringComparer.OrdinalIgnoreCase));

    private static readonly (string[] Keys, NpcInstance Blue, NpcInstance? Red, string Label)[] InstanceAliases =
    [
        (["smok", "drake", "dragon", "smokognia"], GameNpcs.ARENA_DRAKE, null, "Smok Ognia"),
        (["smok-n"], GameNpcs.STRUCT_DRAGON_BLUE, null, "Głowa smoka (niebiescy)"),
        (["smok-c"], GameNpcs.STRUCT_DRAGON_RED, null, "Głowa smoka (czerwoni)"),
        (["wieza", "turret", "tower"], GameNpcs.TOWER_BLUE, GameNpcs.TOWER_RED, "Wieża"),
        (["wieza-n"], GameNpcs.TOWER_BLUE, null, "Wieża niebieskich"),
        (["wieza-c"], GameNpcs.TOWER_RED, null, "Wieża czerwonych"),
        (["nexus"], GameNpcs.STRUCT_NEXUS_BLUE, GameNpcs.STRUCT_NEXUS_RED, "Nexus"),
        (["nexus-n"], GameNpcs.STRUCT_NEXUS_BLUE, null, "Nexus niebieskich"),
        (["nexus-c"], GameNpcs.STRUCT_NEXUS_RED, null, "Nexus czerwonych"),
        (["inhibitor"], GameNpcs.STRUCT_INHIBITOR_BLUE, GameNpcs.STRUCT_INHIBITOR_RED, "Inhibitor"),
        (["inhibitor-n"], GameNpcs.STRUCT_INHIBITOR_BLUE, null, "Inhibitor niebieskich"),
        (["inhibitor-c"], GameNpcs.STRUCT_INHIBITOR_RED, null, "Inhibitor czerwonych"),
        (["minion", "siepacz", "wojownik"], GameNpcs.MINIONORC_BLUE, GameNpcs.MINIONORC_RED, "Siepacz"),
        (["szaman", "mag-minion", "minion-mag"], GameNpcs.MINIONMAGE_BLUE, GameNpcs.MINIONMAGE_RED, "Mag-minion"),
        (["kanonier", "armata", "cannon"], GameNpcs.MINIONCANNON_BLUE, GameNpcs.MINIONCANNON_RED, "Armata"),
    ];

    private static (NpcInstance Instance, string Label)? ResolveInstance(string name, Faction team)
    {
        string key = name.Trim().ToLowerInvariant();
        foreach (var (keys, blue, red, label) in InstanceAliases)
            if (keys.Contains(key)) return (team == Faction.Red && red is { } other ? other : blue, label);
        if (Bestiary.Find(key) is { } kind) return (kind.Instance, kind.DisplayName);
        if (GameInstances.Value.TryGetValue(key, out NpcInstance raw) || GameInstances.Value.TryGetValue(key.Replace('-', '_'), out raw)) return (raw, raw.Value);
        return null;
    }

    private string InstanceHelp(Player target)
    {
        string aliases = string.Join(", ", InstanceAliases.Select(a => a.Keys[0]));
        string mobs = string.Join(", ", Bestiary.All.Select(k => k.Key));
        return $"{target.Name} jest teraz: {target.Instance.Value}. Użycie: /instance <nazwa> [nick|id] — nazwy: {aliases}; moby: {mobs}; albo dowolna instancja z gry (np. LURKER, DRAGON_FIRE). " +
               "/instance reset wraca do postaci.";
    }

    [Command("instance", Description = "zmienia cię (albo gracza) w moba, smoka, wieżę itd.: /instance <nazwa> [nick|id]; /instance reset wraca do postaci; bez nazwy lista (admin)", Aliases = ["przemien", "morph"], Permission = "admin")]
    private string AdminInstance(Player player, string name = "", string who = "")
    {
        Player? target = who.Length == 0 ? player : admin.Find(who);
        if (target is null) return "Nie ma takiego gracza (albo nick jest niejednoznaczny).";
        if (name.Trim().Length == 0) return InstanceHelp(target);
        if (name.Trim().ToLowerInvariant() is "reset" or "ja" or "hero" or "pc_hero" or "postac") return InstanceRestore(player, target);
        if (target.IsDead) return $"{target.Name} nie żyje.";
        if (ResolveInstance(name, Teams.Of(target)) is not { } found) return $"Nieznana instancja „{name.Trim()}”. " + InstanceHelp(target);

        if (!HeroSetup.MorphOwnLook.ContainsKey(target.Id))
        {
            try { HeroSetup.MorphOwnLook[target.Id] = target.Visual; } catch (Exception) {  }
        }
        HeroSetup.SheatheWeapon(target);
        try { target.Instance = found.Instance; }
        catch (Exception error)
        {
            server.Log($"[ADMIN] {player.Name}: /instance {found.Instance.Value} nie powiodło się: {error.Message}");
            return $"Nie udało się: {error.Message}";
        }
        try { target.Health = target.MaxHealth; } catch (Exception) {  }
        try { target.DrawWeapon(WeaponMode.Fists); } catch (Exception) {  }
        server.Log($"[ADMIN] {player.Name}: {target.Name} -> instancja {found.Instance.Value}");
        _ = LogMorphHeight(target, found.Instance.Value);
        return target.Id == player.Id ? $"Jesteś teraz: {found.Label} ({found.Instance.Value}). /instance reset wraca do postaci." : $"{target.Name} jest teraz: {found.Label}.";
    }

    private async Task LogMorphHeight(Player target, string instance)
    {
        await server.Delay(TimeSpan.FromSeconds(3));
        if (!target.IsValid) return;
        Position at = target.Position;
        string line = $"[{DateTime.Now:HH:mm:ss}] ADMIN {target.Name} jako {instance} stoi na pozycji {at.X:0} {at.Y:0} {at.Z:0} (po 3 s; Y = środek modelu nad podłożem + wysokość terenu)";
        server.Log(line);
        try { if (Directory.Exists("logs")) File.AppendAllText(Path.Combine("logs", "botarena-match.log"), line + Environment.NewLine); }
        catch (Exception) {  }
    }

    private string InstanceRestore(Player admin, Player target)
    {
        HeroSetup.SheatheWeapon(target);
        try { target.Instance = NpcInstance.Hero; }
        catch (Exception error) { return $"Nie udało się: {error.Message}"; }
        if (HeroSetup.MorphOwnLook.Remove(target.Id, out PlayerVisual? own))
        {
            try { target.SetVisual(own); } catch (Exception) {  }
        }
        if (matches.Of(target) is { } match && match.Players.TryGetValue(target.Id, out MatchPlayer? mp) && HeroCatalog.Find(mp.Hero) is { } hero)
            HeroSetup.Apply(target, hero, mp);
        else
        {
            target.MaxHealth = ArenaSettings.PlayerHealth;
            target.Health = ArenaSettings.PlayerHealth;
        }
        server.Log($"[ADMIN] {admin.Name}: {target.Name} wraca do postaci");
        return target.Id == admin.Id ? "Wróciłeś do postaci." : $"{target.Name} wraca do postaci.";
    }
}
