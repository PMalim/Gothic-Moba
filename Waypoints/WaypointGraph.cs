using System.Text.Json;

namespace BotArena.Waypoints;

public sealed class WaypointNode
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Position At => new(X, Y, Z);
}

public sealed class WaypointFile
{
    public List<WaypointNode> Nodes { get; set; } = [];
    public List<int[]> Links { get; set; } = [];
}

public sealed class WaypointGraph
{
    public const int MaxNodes = 3000;
    public const int MaxName = 40;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private readonly Dictionary<int, WaypointNode> nodes = [];
    private readonly HashSet<(int, int)> links = [];
    private Dictionary<int, List<int>>? adjacency;
    private int nextId = 1;

    public IReadOnlyCollection<WaypointNode> Nodes => nodes.Values;
    public IReadOnlyCollection<(int A, int B)> Links => links;
    public int Count => nodes.Count;
    public int Version { get; private set; }

    public WaypointNode? Find(int id) => nodes.GetValueOrDefault(id);
    public WaypointNode? FindByName(string name) => nodes.Values.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    public bool HasLink(int a, int b) => links.Contains(Key(a, b));

    public IReadOnlyList<int> Neighbors(int id)
    {
        adjacency ??= BuildAdjacency();
        return adjacency.TryGetValue(id, out List<int>? list) ? list : [];
    }

    private Dictionary<int, List<int>> BuildAdjacency()
    {
        var map = new Dictionary<int, List<int>>();
        foreach ((int a, int b) in links)
        {
            if (!map.TryGetValue(a, out List<int>? la)) map[a] = la = [];
            if (!map.TryGetValue(b, out List<int>? lb)) map[b] = lb = [];
            la.Add(b);
            lb.Add(a);
        }
        return map;
    }

    private void Changed() { adjacency = null; Version++; }

    public string UniqueName(string? wanted, int forId = -1)
    {
        string baseName = Clean(wanted);
        if (baseName.Length == 0) baseName = $"wp-{(forId >= 0 ? forId : nextId)}";
        string name = baseName;
        for (int n = 2; nodes.Values.Any(o => o.Id != forId && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = $"{baseName}-{n}";
        return name;
    }

    private static string Clean(string? text)
    {
        string t = (text ?? "").Trim();
        return t.Length > MaxName ? t[..MaxName] : t;
    }

    public WaypointNode? Add(string? name, float x, float y, float z)
    {
        if (nodes.Count >= MaxNodes || !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) return null;
        var node = new WaypointNode { Id = nextId++, X = x, Y = y, Z = z };
        node.Name = UniqueName(name, node.Id);
        nodes[node.Id] = node;
        Changed();
        return node;
    }

    public bool Remove(int id)
    {
        if (!nodes.Remove(id)) return false;
        links.RemoveWhere(l => l.Item1 == id || l.Item2 == id);
        Changed();
        return true;
    }

    public bool Move(int id, float x, float y, float z)
    {
        if (!nodes.TryGetValue(id, out WaypointNode? node) || !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) return false;
        node.X = x; node.Y = y; node.Z = z;
        Changed();
        return true;
    }

    public bool Rename(int id, string name)
    {
        if (!nodes.TryGetValue(id, out WaypointNode? node)) return false;
        string clean = Clean(name);
        if (clean.Length == 0 || nodes.Values.Any(o => o.Id != id && string.Equals(o.Name, clean, StringComparison.OrdinalIgnoreCase))) return false;
        node.Name = clean;
        Changed();
        return true;
    }

    public bool Link(int a, int b)
    {
        if (a == b || !nodes.ContainsKey(a) || !nodes.ContainsKey(b)) return false;
        bool added = links.Add(Key(a, b));
        if (added) Changed();
        return added;
    }

    public bool Unlink(int a, int b)
    {
        bool removed = links.Remove(Key(a, b));
        if (removed) Changed();
        return removed;
    }

    public bool Toggle(int a, int b)
    {
        if (HasLink(a, b)) { Unlink(a, b); return false; }
        return Link(a, b);
    }

    public void Clear()
    {
        nodes.Clear();
        links.Clear();
        nextId = 1;
        Changed();
    }

    public string ToJson() => JsonSerializer.Serialize(new WaypointFile
    {
        Nodes = [.. nodes.Values.OrderBy(n => n.Id)],
        Links = [.. links.OrderBy(l => l.Item1).ThenBy(l => l.Item2).Select(l => new[] { l.Item1, l.Item2 })],
    }, Json);

    public bool LoadJson(string json)
    {
        try
        {
            WaypointFile? file = JsonSerializer.Deserialize<WaypointFile>(json, Json);
            if (file is null) return false;
            var loaded = new Dictionary<int, WaypointNode>();
            foreach (WaypointNode n in file.Nodes)
            {
                if (n.Id <= 0 || loaded.ContainsKey(n.Id) || !float.IsFinite(n.X + n.Y + n.Z)) continue;
                n.Name = Clean(n.Name);
                loaded[n.Id] = n;
            }
            nodes.Clear();
            links.Clear();
            foreach (WaypointNode n in loaded.Values)
            {
                if (n.Name.Length == 0 || nodes.Values.Any(o => string.Equals(o.Name, n.Name, StringComparison.OrdinalIgnoreCase))) n.Name = $"wp-{n.Id}";
                nodes[n.Id] = n;
            }
            foreach (int[] l in file.Links)
                if (l.Length == 2 && l[0] != l[1] && nodes.ContainsKey(l[0]) && nodes.ContainsKey(l[1])) links.Add(Key(l[0], l[1]));
            nextId = nodes.Count == 0 ? 1 : nodes.Keys.Max() + 1;
            Changed();
            return true;
        }
        catch (JsonException) { return false; }
    }
}
