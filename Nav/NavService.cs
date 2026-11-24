using System.Diagnostics;
using System.Text.Json;
using BotArena.Lobby;

namespace BotArena.Nav;

public sealed record NavStroke(float[] Points, float Radius, byte Cost);

public sealed class NavService(Server server, LobbyService lobby, Func<Player, bool> isAdmin)
{
    private const string Folder = "gamemodes/BotArena/nav";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };
    private readonly List<NavStroke> strokes = [];
    private byte[] baseCost = [], baseMask = [];
    private short[] baseGround = [];
    private const int OpenStep = 160;

    private readonly Dictionary<PlayerId, int> viewers = [];
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    public void Start() => server.Timers.Every(TimeSpan.FromMilliseconds(500), PublishView);

    public bool ToggleView(Player player, int radius)
    {
        if (viewers.Remove(player.Id)) { server.Network.Send(player, new ArenaNavViewMessage(0, 0, 0, 0, 0, "")); return false; }
        viewers[player.Id] = Math.Clamp(radius, 4, 30);
        return true;
    }

    public void Forget(Player player) => viewers.Remove(player.Id);

    private void PublishView()
    {
        if (Grid is null || viewers.Count == 0) return;
        foreach ((PlayerId id, int radius) in viewers.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true } player) { viewers.Remove(id); continue; }
            Position at = player.Position;
            (int ci, int cj) = Grid.CellOf(at.X, at.Z);
            int width = radius * 2 + 1;
            var text = new System.Text.StringBuilder(width * width * 3);
            for (int j = cj - radius; j <= cj + radius; j++)
            {
                for (int i = ci - radius; i <= ci + radius; i++)
                {
                    if (!Grid.InRange(i, j) || Grid.Mask[Grid.Index(i, j)] == 0 || Grid.Ground[Grid.Index(i, j)] == short.MinValue) { text.Append("000"); continue; }
                    int n = Grid.Index(i, j);
                    byte cost = Grid.Cost[n];
                    char kind = cost switch { NavCost.Blocked => '6', NavCost.Path => '3', NavCost.Rough => '4', NavCost.Open => '5', _ => Grid.Mask[n] == 255 ? '1' : '2' };
                    int height = Math.Clamp((int)MathF.Round((Grid.Ground[n] - at.Y) / 10f) + 648, 0, 1295);
                    text.Append(kind).Append(Digits[height / 36]).Append(Digits[height % 36]);
                }
            }
            server.Network.Send(player, new ArenaNavViewMessage(Grid.CenterX(ci - radius), Grid.CenterZ(cj - radius), Grid.Cell, width, at.Y, text.ToString()));
        }
    }

    public Waypoints.WaypointService? Waypoints { get; set; }

    public MapEdit.MapObjectService? MapObjects { get; set; }

    public NavGrid? Grid { get; private set; }
    public NavRouter? Router { get; private set; }
    public bool Ready => Router is not null;

    public void Load()
    {
        string[] candidates = [Path.Combine(Folder, "navgrid.nav"), Path.Combine(AppContext.BaseDirectory, Folder, "navgrid.nav")];
        string? file = candidates.FirstOrDefault(File.Exists);
        if (file is null) { server.Log("[NAV] brak navgrid.nav — trasy po waypointach świata"); return; }
        try
        {
            Grid = NavGrid.Load(file);
            baseCost = (byte[])Grid.Cost.Clone();
            baseMask = (byte[])Grid.Mask.Clone();
            baseGround = (short[])Grid.Ground.Clone();
            Router = new NavRouter(Grid);
            string edits = Path.Combine(Path.GetDirectoryName(file)!, "nav-edits.json");
            if (File.Exists(edits) && JsonSerializer.Deserialize<List<NavStroke>>(File.ReadAllText(edits), Json) is { } saved) strokes.AddRange(saved);
            Rebuild();
            server.Log($"[NAV] siatka {Grid.Width}x{Grid.Height}, poprawek {strokes.Count}");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException)
        {
            server.Log("[NAV] nie wczytano siatki: " + e.Message);
            Grid = null; Router = null;
        }
    }

    public IReadOnlyList<Position>? Route(Position from, Position to, int maxPoints = 40) => Router?.Route(from, to, maxPoints);

    private void Rebuild()
    {
        if (Grid is null) return;
        Array.Copy(baseCost, Grid.Cost, baseCost.Length);
        Array.Copy(baseMask, Grid.Mask, baseMask.Length);
        Array.Copy(baseGround, Grid.Ground, baseGround.Length);
        foreach (NavStroke stroke in strokes) Paint(Grid, stroke);
        ApplyOpen(Grid);
    }

    private static void ApplyOpen(NavGrid grid)
    {
        if (grid.Floor is not { } floor) return;
        var open = new List<int>();
        for (int n = 0; n < grid.Cost.Length; n++)
        {
            if (grid.Cost[n] != NavCost.Open || floor[n] == short.MinValue) continue;
            grid.Ground[n] = floor[n];
            open.Add(n);
        }
        bool Usable(int i, int j) => grid.InRange(i, j) && grid.Cost[grid.Index(i, j)] != NavCost.Blocked && (grid.Mask[grid.Index(i, j)] != 0 || (grid.Cost[grid.Index(i, j)] == NavCost.Open && floor[grid.Index(i, j)] != short.MinValue));
        foreach (int n in open)
        {
            int i = n % grid.Width, j = n / grid.Width;
            for (int d = 0; d < 8; d++)
            {
                int ni = i + NavGrid.Dx[d], nj = j + NavGrid.Dz[d];
                if (!Usable(ni, nj) || Math.Abs(grid.Ground[grid.Index(ni, nj)] - grid.Ground[n]) > OpenStep) continue;
                if (d % 2 == 1 && (!Usable(i + NavGrid.Dx[d], j) || !Usable(i, j + NavGrid.Dz[d]))) continue;
                grid.Mask[n] |= (byte)(1 << d);
                grid.Mask[grid.Index(ni, nj)] |= (byte)(1 << ((d + 4) % 8));
            }
        }
    }

    public static void Paint(NavGrid grid, NavStroke stroke)
    {
        float[] p = stroke.Points;
        float r = stroke.Radius, step = grid.Cell * 0.5f;
        for (int n = 0; n + 1 < p.Length; n += 2)
        {
            float x0 = p[n], z0 = p[n + 1];
            float x1 = n + 3 < p.Length ? p[n + 2] : x0, z1 = n + 3 < p.Length ? p[n + 3] : z0;
            int samples = Math.Max(1, (int)(MathF.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)) / step) + 1);
            for (int s = 0; s <= samples; s++)
            {
                float t = s / (float)samples, x = x0 + (x1 - x0) * t, z = z0 + (z1 - z0) * t;
                (int ci, int cj) = grid.CellOf(x, z);
                int reach = (int)MathF.Ceiling(r / grid.Cell) + 1;
                for (int j = cj - reach; j <= cj + reach; j++)
                {
                    for (int i = ci - reach; i <= ci + reach; i++)
                    {
                        if (!grid.InRange(i, j)) continue;
                        float dx = grid.CenterX(i) - x, dz = grid.CenterZ(j) - z;
                        if (dx * dx + dz * dz <= r * r) grid.Cost[grid.Index(i, j)] = stroke.Cost;
                    }
                }
            }
        }
    }

    private void Save()
    {
        try
        {
            if (!System.IO.Directory.Exists(Folder)) return;
            File.WriteAllText(Path.Combine(Folder, "nav-edits.json"), JsonSerializer.Serialize(strokes, Json));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Handle(Player player, string action, string json)
    {
        if (!isAdmin(player)) { lobby.Toast(player, "Edytor nawigacji tylko dla admina.", "error"); return; }
        if (action.StartsWith("nav.obj.", StringComparison.Ordinal))
        {
            JsonElement objArgs = default;
            try { if (!string.IsNullOrWhiteSpace(json)) objArgs = JsonDocument.Parse(json).RootElement.Clone(); }
            catch (JsonException) { return; }
            MapObjects?.Handle(player, action, objArgs);
            return;
        }
        if (Grid is null) { lobby.Toast(player, "Brak siatki nawigacji (navgrid.nav w gamemodes/BotArena/nav).", "error"); return; }
        JsonElement args = default;
        try { if (!string.IsNullOrWhiteSpace(json)) args = JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return; }
        switch (action)
        {
            case "nav.open":
                lobby.Push(player, "nav", new { cell = Grid.Cell, originX = Grid.OriginX, originZ = Grid.OriginZ, strokes = strokes.Select(Dto).ToArray() });
                break;
            case "nav.paint":
                if (Parse(args) is { } stroke)
                {
                    strokes.Add(stroke);
                    Paint(Grid, stroke);
                    Save();
                }
                break;
            case "nav.undo":
                if (strokes.Count > 0)
                {
                    strokes.RemoveAt(strokes.Count - 1);
                    Rebuild();
                    Save();
                }
                lobby.Push(player, "nav.strokes", new { strokes = strokes.Select(Dto).ToArray() });
                break;
            case "nav.clear":
                strokes.Clear();
                Rebuild();
                Save();
                lobby.Push(player, "nav.strokes", new { strokes = Array.Empty<object>() });
                break;
            case "nav.close":
                break;
            case "nav.route":
                TestRoute(player, args);
                break;
            default:
                if (action.StartsWith("nav.wp.", StringComparison.Ordinal)) Waypoints?.Handle(player, action, args);
                break;
        }
    }

    private void TestRoute(Player player, JsonElement args)
    {
        if (!TryPoint(args, "a", out float ax, out float az) || !TryPoint(args, "b", out float bx, out float bz)) return;
        float ay = Grid!.HeightAt(ax, az) ?? 0f, by = Grid.HeightAt(bx, bz) ?? 0f;
        var watch = Stopwatch.StartNew();
        IReadOnlyList<Position>? route = Route(new Position(ax, ay, az), new Position(bx, by, bz));
        lobby.Push(player, "nav.route", new { points = route?.SelectMany(p => new[] { p.X, p.Z }).ToArray(), ms = watch.Elapsed.TotalMilliseconds });
    }

    private static bool TryPoint(JsonElement args, string name, out float x, out float z)
    {
        x = z = 0;
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out JsonElement a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 2) return false;
        x = a[0].GetSingle(); z = a[1].GetSingle();
        return float.IsFinite(x) && float.IsFinite(z);
    }

    private static object Dto(NavStroke s) => new { pts = s.Points, r = s.Radius, cost = s.Cost };

    private static NavStroke? Parse(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("pts", out JsonElement pts) || pts.ValueKind != JsonValueKind.Array) return null;
        if (!args.TryGetProperty("r", out JsonElement r) || !args.TryGetProperty("cost", out JsonElement cost)) return null;
        int count = pts.GetArrayLength();
        if (count < 2 || count > 4000 || count % 2 != 0) return null;
        var points = new float[count];
        int n = 0;
        foreach (JsonElement v in pts.EnumerateArray()) points[n++] = v.GetSingle();
        byte kind = (byte)cost.GetInt32();
        if (kind is not (NavCost.Normal or NavCost.Path or NavCost.Rough or NavCost.Open or NavCost.Blocked) || points.Any(v => !float.IsFinite(v))) return null;
        return new NavStroke(points, Math.Clamp(r.GetSingle(), 50f, 1500f), kind);
    }
}
