using System.Text.Json;
using BotArena.Lobby;
using BotArena.Matches;
using BotArena.Nav;

namespace BotArena.MapEdit;

public sealed class MapObjectService(NavService nav, LobbyService lobby, MatchService matches)
{
    private const string File = "gamemodes/BotArena/nav/map-objects.json";
    private const float Limit = 200000f;

    private sealed record Entry(string Id, LaneDef Lane, int Index, TurretDef Def);

    private readonly Dictionary<string, TurretDef> defaultStructures = [];
    private readonly Dictionary<string, Position[]> defaultPaths = [];

    private static IEnumerable<Entry> Entries()
    {
        foreach (LaneDef lane in LaneSettings.Lanes)
        {
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < lane.Turrets.Count; i++)
            {
                TurretDef def = lane.Turrets[i];
                string stem = $"{lane.Name}|{def.Team}|{def.Kind}|{def.Tier}";
                int n = seen[stem] = seen.GetValueOrDefault(stem) + 1;
                yield return new Entry($"s|{stem}|{n}", lane, i, def);
            }
        }
    }

    private static string Describe(Entry e)
    {
        string team = e.Def.Team == Faction.Blue ? "niebieskich" : "czerwonych";
        string what = e.Def.Kind switch
        {
            StructureKind.Inhibitor => "Inhibitor",
            StructureKind.Nexus => "Nexus",
            StructureKind.Dragon => "Głowa smoka",
            _ when e.Def.Tier == 0 => "Wieża nexusa",
            _ => $"Wieża {e.Def.Tier}",
        };
        return $"{what} {team} ({e.Lane.Name})";
    }

    private static string KindOf(TurretDef def) => def.Kind switch
    {
        StructureKind.Inhibitor => "inhibitor",
        StructureKind.Nexus => "nexus",
        StructureKind.Dragon => "dragon",
        _ => "turret",
    };

    private static bool Same(Position a, Position b) => MathF.Abs(a.X - b.X) < 0.5f && MathF.Abs(a.Y - b.Y) < 0.5f && MathF.Abs(a.Z - b.Z) < 0.5f;

    private static bool Same(Position[] a, Position[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (!Same(a[i], b[i])) return false;
        return true;
    }

    public void Load()
    {
        foreach (Entry e in Entries()) defaultStructures.TryAdd(e.Id, e.Def);
        foreach (LaneDef lane in LaneSettings.Lanes) defaultPaths.TryAdd(lane.Name, [.. lane.RouteOf(Faction.Blue)]);
        string[] candidates = [File, Path.Combine(AppContext.BaseDirectory, File)];
        string? existing = candidates.FirstOrDefault(System.IO.File.Exists);
        if (existing is null) return;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(existing));
            int structures = 0, paths = 0;
            if (doc.RootElement.TryGetProperty("structures", out JsonElement s) && s.ValueKind == JsonValueKind.Object)
            {
                Dictionary<string, Entry> byId = Entries().ToDictionary(e => e.Id);
                foreach (JsonProperty p in s.EnumerateObject())
                {
                    if (!byId.TryGetValue(p.Name, out Entry? e) || p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() != 4) continue;
                    float[] v = [.. p.Value.EnumerateArray().Select(x => x.GetSingle())];
                    if (v.Any(f => !float.IsFinite(f))) continue;
                    e.Lane.SetTurret(e.Index, e.Def with { At = new Position(v[0], v[1], v[2]), Angle = v[3] });
                    structures++;
                }
            }
            if (doc.RootElement.TryGetProperty("lanes", out JsonElement l) && l.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in l.EnumerateObject())
                {
                    LaneDef? lane = LaneSettings.Lanes.FirstOrDefault(x => x.Name == p.Name);
                    if (lane is null || p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() < 2) continue;
                    var points = new List<Position>();
                    foreach (JsonElement pt in p.Value.EnumerateArray())
                        if (pt.ValueKind == JsonValueKind.Array && pt.GetArrayLength() == 3)
                            points.Add(new Position(pt[0].GetSingle(), pt[1].GetSingle(), pt[2].GetSingle()));
                    if (points.Count >= 2) { lane.SetPoints([.. points]); paths++; }
                }
            }
            Console.WriteLine($"[OBIEKTY] poprawki z {existing}: struktur {structures}, tras {paths}");
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or FormatException)
        {
            Console.WriteLine("[OBIEKTY] nie wczytano map-objects.json: " + e.Message);
        }
    }

    private void Save()
    {
        try
        {
            var structures = new Dictionary<string, float[]>();
            foreach (Entry e in Entries())
                if (defaultStructures.TryGetValue(e.Id, out TurretDef? d) && !(Same(d.At, e.Def.At) && MathF.Abs(d.Angle - e.Def.Angle) < 0.01f))
                    structures[e.Id] = [e.Def.At.X, e.Def.At.Y, e.Def.At.Z, e.Def.Angle];
            var lanes = new Dictionary<string, float[][]>();
            foreach (LaneDef lane in LaneSettings.Lanes)
            {
                Position[] now = lane.RouteOf(Faction.Blue);
                if (defaultPaths.TryGetValue(lane.Name, out Position[]? original) && !Same(now, original))
                    lanes[lane.Name] = [.. now.Select(p => new[] { p.X, p.Y, p.Z })];
            }
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(new { structures, lanes }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private float? Ground(float x, float z)
    {
        if (nav.Grid is not { } grid) return null;
        float? h = grid.HeightAt(x, z);
        if (h is null && grid.Nearest(x, z) is { } cell) h = grid.Ground[grid.Index(cell.Item1, cell.Item2)];
        return h is { } v && v > short.MinValue ? v : null;
    }

    private float NewY(Position old, float x, float z) =>
        Ground(old.X, old.Z) is { } before && Ground(x, z) is { } after ? old.Y + (after - before) : old.Y;

    public void Handle(Player player, string action, JsonElement args)
    {
        switch (action)
        {
            case "nav.obj.open":
                Push(player);
                return;
            case "nav.obj.move" when Text(args, "id") is { } id && Num(args, "x") is { } x && Num(args, "z") is { } z:
                Move(player, id, x, z);
                return;
            case "nav.obj.angle" when Text(args, "id") is { } aid && Num(args, "angle") is { } angle:
                Rotate(player, aid, angle);
                return;
            case "nav.obj.addpoint" when Text(args, "lane") is { } lane && Int(args, "after") is { } after && Num(args, "x") is { } ax && Num(args, "z") is { } az:
                AddPoint(player, lane, after, ax, az);
                return;
            case "nav.obj.delpoint" when Text(args, "id") is { } did:
                DeletePoint(player, did);
                return;
            case "nav.obj.reset" when Text(args, "id") is { } rid:
                Reset(player, rid);
                return;
        }
    }

    private void Move(Player player, string id, float x, float z)
    {
        if (Math.Abs(x) > Limit || Math.Abs(z) > Limit) return;
        if (Entries().FirstOrDefault(e => e.Id == id) is { } entry)
        {
            TurretDef moved = entry.Def with { At = new Position(x, NewY(entry.Def.At, x, z), z) };
            entry.Lane.SetTurret(entry.Index, moved);
            ApplyLive(entry.Lane, entry.Index, moved);
            Save();
            Push(player, id);
            return;
        }
        if (ParsePoint(id) is { } point)
        {
            (LaneDef lane, int index) = point;
            Position[] points = [.. lane.RouteOf(Faction.Blue)];
            points[index] = new Position(x, NewY(points[index], x, z), z);
            lane.SetPoints(points);
            Save();
            Push(player, id);
        }
    }

    private void Rotate(Player player, string id, float angle)
    {
        if (!float.IsFinite(angle) || Entries().FirstOrDefault(e => e.Id == id) is not { } entry) return;
        TurretDef turned = entry.Def with { Angle = ((angle % 360f) + 360f) % 360f };
        entry.Lane.SetTurret(entry.Index, turned);
        ApplyLive(entry.Lane, entry.Index, turned);
        Save();
        Push(player, id);
    }

    private void AddPoint(Player player, string laneName, int after, float x, float z)
    {
        LaneDef? lane = LaneSettings.Lanes.FirstOrDefault(l => l.Name == laneName);
        if (lane is null || Math.Abs(x) > Limit || Math.Abs(z) > Limit) return;
        List<Position> points = [.. lane.RouteOf(Faction.Blue)];
        if (after < 0 || after >= points.Count - 1) { lobby.Toast(player, "Punkt dodaje się między dwoma punktami trasy.", "error"); return; }
        Position near = points[after];
        points.Insert(after + 1, new Position(x, NewY(near, x, z), z));
        lane.SetPoints([.. points]);
        Save();
        Push(player, $"p|{lane.Name}|{after + 1}");
    }

    private void DeletePoint(Player player, string id)
    {
        if (ParsePoint(id) is not { } point) return;
        (LaneDef lane, int index) = point;
        List<Position> points = [.. lane.RouteOf(Faction.Blue)];
        if (index == 0 || index == points.Count - 1) { lobby.Toast(player, "Pierwszy i ostatni punkt trasy to spawny baz — nie da się ich usunąć.", "error"); return; }
        points.RemoveAt(index);
        lane.SetPoints([.. points]);
        Save();
        Push(player);
    }

    private void Reset(Player player, string id)
    {
        if (Entries().FirstOrDefault(e => e.Id == id) is { } entry && defaultStructures.TryGetValue(id, out TurretDef? original))
        {
            entry.Lane.SetTurret(entry.Index, original);
            ApplyLive(entry.Lane, entry.Index, original);
            Save();
            Push(player, id);
            return;
        }
        if (ParsePoint(id) is { } point && defaultPaths.TryGetValue(point.Lane.Name, out Position[]? path))
        {
            point.Lane.SetPoints([.. path]);
            Save();
            Push(player);
        }
    }

    private void ApplyLive(LaneDef lane, int index, TurretDef def)
    {
        foreach (Match match in matches.All.ToArray())
            match.Director?.MoveStructure(lane, index, def);
    }

    private static (LaneDef Lane, int Index)? ParsePoint(string id)
    {
        string[] parts = id.Split('|');
        if (parts.Length != 3 || parts[0] != "p" || !int.TryParse(parts[2], out int index)) return null;
        LaneDef? lane = LaneSettings.Lanes.FirstOrDefault(l => l.Name == parts[1]);
        return lane is not null && index >= 0 && index < lane.RouteOf(Faction.Blue).Length ? (lane, index) : null;
    }

    private void Push(Player player, string selected = "")
    {
        var items = new List<object>();
        foreach (Entry e in Entries())
        {
            bool changed = defaultStructures.TryGetValue(e.Id, out TurretDef? d) && !(Same(d.At, e.Def.At) && MathF.Abs(d.Angle - e.Def.Angle) < 0.01f);
            items.Add(new
            {
                id = e.Id, k = KindOf(e.Def), n = Describe(e), lane = e.Lane.Name, team = e.Def.Team == Faction.Blue ? 0 : 1,
                x = (int)MathF.Round(e.Def.At.X), y = (int)MathF.Round(e.Def.At.Y), z = (int)MathF.Round(e.Def.At.Z),
                a = (int)MathF.Round(e.Def.Angle), t = e.Def.Tier, c = changed,
            });
        }
        foreach (LaneDef lane in LaneSettings.Lanes)
        {
            Position[] points = lane.RouteOf(Faction.Blue);
            bool changed = defaultPaths.TryGetValue(lane.Name, out Position[]? original) && !Same(points, original);
            for (int i = 0; i < points.Length; i++)
                items.Add(new
                {
                    id = $"p|{lane.Name}|{i}", k = "point", n = i == 0 ? $"{lane.Name}: spawn niebieskich" : i == points.Length - 1 ? $"{lane.Name}: spawn czerwonych" : $"{lane.Name}: punkt {i + 1}",
                    lane = lane.Name, team = -1, x = (int)MathF.Round(points[i].X), y = (int)MathF.Round(points[i].Y), z = (int)MathF.Round(points[i].Z),
                    a = 0, t = 0, i, last = i == points.Length - 1, c = changed,
                });
        }
        lobby.Push(player, "obj", new { selected, items });
    }

    private static float? Num(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && float.IsFinite(v.GetSingle()) ? v.GetSingle() : null;

    private static int? Int(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;

    private static string? Text(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
