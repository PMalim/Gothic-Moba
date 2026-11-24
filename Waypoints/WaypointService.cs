using System.Diagnostics;
using System.Text.Json;
using BotArena.Lobby;
using BotArena.Nav;

namespace BotArena.Waypoints;

public sealed class WaypointService(Server server, NavService nav, LobbyService lobby)
{
    private const string File = "gamemodes/BotArena/nav/waypoints.json";
    private const int UndoLimit = 40;
    private readonly WaypointGraph graph = new();
    private readonly LinkedList<string> undo = new();
    private WaypointRouter? router;
    private readonly HashSet<PlayerId> worldEditors = [];

    public WaypointGraph Graph => graph;
    public bool Ready => router is not null && graph.Count > 0;

    public void Load()
    {
        router = new WaypointRouter(graph, Visible);
        string[] candidates = [File, System.IO.Path.Combine(AppContext.BaseDirectory, File)];
        string? existing = candidates.FirstOrDefault(System.IO.File.Exists);
        if (existing is not null && graph.LoadJson(System.IO.File.ReadAllText(existing)))
        {
            Console.WriteLine($"[WAYPOINTY] wczytano {graph.Count} punktów, {graph.Links.Count} połączeń ({existing})");
            return;
        }
        if (nav.Grid is null || nav.Router is null) { Console.WriteLine("[WAYPOINTY] brak pliku waypoints.json i siatki — NPC jadą po siatce / waypointach świata"); return; }
        Generate();
    }

    public int Generate()
    {
        if (nav.Grid is null || nav.Router is null) return 0;
        Remember();
        var lanes = LaneSettings.Lanes.Select(l => (l.Name, l.RouteOf(Faction.Blue)));
        WaypointGenerator.Generate(graph, nav.Grid, nav.Router, Visible, lanes);
        Save();
        Console.WriteLine($"[WAYPOINTY] wygenerowano {graph.Count} punktów, {graph.Links.Count} połączeń");
        return graph.Count;
    }

    private void Save()
    {
        try
        {
            string dir = System.IO.Path.GetDirectoryName(File)!;
            Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(File, graph.ToJson());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public IReadOnlyList<Position>? Route(Position from, Position to, int maxPoints = 48) => Ready ? router!.Route(from, to, maxPoints) : null;

    public bool Visible(Position a, Position b)
    {
        NavGrid? grid = nav.Grid;
        if (grid is null) return true;
        float dx = b.X - a.X, dz = b.Z - a.Z, length = MathF.Sqrt(dx * dx + dz * dz);
        int steps = Math.Max(1, (int)MathF.Ceiling(length / 50f));
        short previous = short.MinValue;
        for (int k = 0; k <= steps; k++)
        {
            float x = a.X + dx * k / steps, z = a.Z + dz * k / steps;
            (int i, int j) = grid.CellOf(x, z);
            if (!grid.InRange(i, j)) return false;
            int n = grid.Index(i, j);
            if (grid.Mask[n] == 0 || grid.Cost[n] == NavCost.Blocked || grid.Ground[n] == short.MinValue) return false;
            short h = grid.Ground[n];
            if (previous != short.MinValue && Math.Abs(h - previous) > 70) return false;
            previous = h;
        }
        return true;
    }

    private void Remember()
    {
        undo.AddLast(graph.ToJson());
        while (undo.Count > UndoLimit) undo.RemoveFirst();
    }

    public void Handle(Player player, string action, JsonElement args)
    {
        switch (action)
        {
            case "nav.wp.open":
                Push(player);
                return;
            case "nav.wp.world":
                worldEditors.Add(player.Id);
                Push(player);
                return;
            case "nav.wp.worldclose":
                worldEditors.Remove(player.Id);
                return;
            case "nav.wp.add":
                if (Num(args, "x") is { } ax && Num(args, "z") is { } az)
                {
                    Remember();
                    if (graph.Add(Text(args, "name"), ax, Num(args, "y") ?? HeightAt(ax, az), az) is { } added)
                    {
                        if (Int(args, "from") is { } from && from >= 0 && graph.Find(from) is not null) graph.Link(from, added.Id);
                        Push(player, added.Id);
                    }
                    else lobby.Toast(player, "Za dużo waypointów.", "error");
                }
                return;
            case "nav.wp.move":
                if (Int(args, "id") is { } mid && Num(args, "x") is { } mx && Num(args, "z") is { } mz)
                {
                    Remember();
                    graph.Move(mid, mx, Num(args, "y") ?? HeightAt(mx, mz), mz);
                    Push(player, mid);
                }
                return;
            case "nav.wp.rename":
                if (Int(args, "id") is { } rid && Text(args, "name") is { } name)
                {
                    Remember();
                    if (!graph.Rename(rid, name)) { undo.RemoveLast(); lobby.Toast(player, "Nazwa pusta albo zajęta.", "error"); }
                    Push(player, rid);
                }
                return;
            case "nav.wp.delete":
                if (Int(args, "id") is { } did)
                {
                    Remember();
                    graph.Remove(did);
                    Push(player);
                }
                return;
            case "nav.wp.link":
                if (Int(args, "a") is { } la && Int(args, "b") is { } lb && la != lb)
                {
                    Remember();
                    graph.Toggle(la, lb);
                    Push(player, lb);
                }
                return;
            case "nav.wp.generate":
                lobby.Toast(player, Generate() > 0 ? $"Wygenerowano {graph.Count} waypointów ({graph.Links.Count} połączeń). Ctrl+Z cofa." : "Brak siatki — nie ma z czego generować.", "info");
                Push(player);
                return;
            case "nav.wp.undo":
                if (undo.Last is { } last)
                {
                    undo.RemoveLast();
                    graph.LoadJson(last.Value);
                    Save();
                }
                Push(player);
                return;
            case "nav.wp.route":
                TestRoute(player, args);
                return;
        }
    }

    private void Push(Player player, int selected = -1)
    {
        Save();
        foreach (PlayerId id in worldEditors.ToArray())
        {
            if (server.Players.Find(id) is { IsValid: true } editor) SendWorld(editor, ReferenceEquals(editor, player) ? selected : -1);
            else worldEditors.Remove(id);
        }
        lobby.Push(player, "wp", new
        {
            selected,
            nodes = graph.Nodes.Select(n => new { id = n.Id, n = n.Name, x = (int)MathF.Round(n.X), y = (int)MathF.Round(n.Y), z = (int)MathF.Round(n.Z) }).ToArray(),
            links = graph.Links.Select(l => new[] { l.A, l.B }).ToArray(),
        });
    }

    private void SendWorld(Player editor, int selected)
    {
        List<WaypointNode> nodes = [.. graph.Nodes.OrderBy(n => n.Id)];
        List<(int A, int B)> links = [.. graph.Links];
        server.Network.Send(editor, new ArenaWaypointsMessage(selected, [.. nodes.Select(n => n.Id)], [.. nodes.Select(n => n.Name)],
            [.. nodes.Select(n => n.X)], [.. nodes.Select(n => n.Y)], [.. nodes.Select(n => n.Z)], [.. links.Select(l => l.A)], [.. links.Select(l => l.B)]));
    }

    private void TestRoute(Player player, JsonElement args)
    {
        if (Point(args, "a") is not { } a || Point(args, "b") is not { } b) return;
        var watch = Stopwatch.StartNew();
        IReadOnlyList<Position>? route = Route(new Position(a.X, HeightAt(a.X, a.Z), a.Z), new Position(b.X, HeightAt(b.X, b.Z), b.Z));
        lobby.Push(player, "wp.route", new { points = route?.SelectMany(p => new[] { p.X, p.Z }).ToArray(), ms = watch.Elapsed.TotalMilliseconds });
    }

    private float HeightAt(float x, float z)
    {
        if (nav.Grid is not { } grid) return 0f;
        if (grid.HeightAt(x, z) is { } h) return h;
        (int, int)? cell = grid.Nearest(x, z);
        return cell is { } c ? grid.Ground[grid.Index(c.Item1, c.Item2)] : 0f;
    }

    private static float? Num(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && float.IsFinite(v.GetSingle()) ? v.GetSingle() : null;

    private static int? Int(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;

    private static string? Text(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static (float X, float Z)? Point(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out JsonElement a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 2) return null;
        float x = a[0].GetSingle(), z = a[1].GetSingle();
        return float.IsFinite(x) && float.IsFinite(z) ? (x, z) : null;
    }
}
