using System.Text.Json;

namespace BotArena.Ai.Memory;

public sealed class MobSnapshot
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int Health { get; set; }
    public double RespawnIn { get; set; } = -1;
    public Dictionary<string, string> States { get; set; } = [];
}

public sealed class MemorySnapshot
{
    public DateTime SavedAtUtc { get; set; }
    public Dictionary<string, MobSnapshot> Mobs { get; set; } = [];
}

public sealed class MemoryStore(string? file)
{
    private const string Slot = "BotArena.AiMemory";
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private MemorySnapshot? loaded;

    public TimeSpan? Age => loaded is null ? null : DateTime.UtcNow - loaded.SavedAtUtc;
    public int Count => loaded?.Mobs.Count ?? 0;

    public int Load()
    {
        string? text = AppDomain.CurrentDomain.GetData(Slot) as string;
        AppDomain.CurrentDomain.SetData(Slot, null);
        if (text is null && file is not null && File.Exists(file))
        {
            try { text = File.ReadAllText(file); } catch (IOException) { }
        }
        if (text is null) return 0;
        try { loaded = JsonSerializer.Deserialize<MemorySnapshot>(text, Json); }
        catch (JsonException) { loaded = null; }
        return Count;
    }

    public MobSnapshot? Take(string key) => loaded is not null && loaded.Mobs.Remove(key, out MobSnapshot? snapshot) ? snapshot : null;

    public static void RestoreStates(MobSnapshot snapshot, MobMemory memory)
    {
        foreach (var (typeName, json) in snapshot.States)
        {
            Type? type = typeof(MemoryStore).Assembly.GetType(typeName);
            if (type is null || !typeof(IPersistentState).IsAssignableFrom(type)) continue;
            try { if (JsonSerializer.Deserialize(json, type, Json) is IPersistentState state) memory.Restore(state); }
            catch (JsonException) { }
        }
    }

    public void Save(IEnumerable<Mob> mobs, TimeSpan now)
    {
        var snapshot = new MemorySnapshot { SavedAtUtc = DateTime.UtcNow };
        foreach (Mob mob in mobs)
        {
            if (mob.Key is not { } key) continue;
            var entry = new MobSnapshot();
            if (mob.Npc is not null)
            {
                entry.X = mob.LastPosition.X; entry.Y = mob.LastPosition.Y; entry.Z = mob.LastPosition.Z;
                entry.Health = mob.LastHealth;
            }
            else entry.RespawnIn = Math.Max(0, (mob.RespawnAt - now).TotalSeconds);
            foreach (IPersistentState state in mob.Memory.PersistentStates)
                entry.States[state.GetType().FullName!] = JsonSerializer.Serialize(state, state.GetType(), Json);
            snapshot.Mobs[key] = entry;
        }
        string text = JsonSerializer.Serialize(snapshot, Json);
        AppDomain.CurrentDomain.SetData(Slot, text);
        if (file is null || Path.GetDirectoryName(Path.GetFullPath(file)) is not { } directory || !Directory.Exists(directory)) return;
        try { File.WriteAllText(file, text); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
