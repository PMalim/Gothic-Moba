using System.Buffers.Binary;

namespace BotArena.Nav;

public static class NavCost
{
    public const byte Normal = 0, Path = 1, Rough = 2, Open = 254, Blocked = 255;

    public static float Multiplier(byte cost) => cost switch { Path => 0.55f, Rough => 2.2f, _ => 1f };
}

public sealed class NavGrid
{
    public static readonly int[] Dx = [1, 1, 0, -1, -1, -1, 0, 1];
    public static readonly int[] Dz = [0, 1, 1, 1, 0, -1, -1, -1];

    public NavGrid(float cell, float originX, float originZ, int width, int height, short[] ground, byte[] mask, byte[] cost, short[]? floor = null)
    {
        Floor = floor;
        Cell = cell; OriginX = originX; OriginZ = originZ; Width = width; Height = height;
        Ground = ground; Mask = mask; Cost = cost;
    }

    public float Cell { get; }
    public float OriginX { get; }
    public float OriginZ { get; }
    public int Width { get; }
    public int Height { get; }
    public short[] Ground { get; }
    public byte[] Mask { get; }
    public short[]? Floor { get; }
    public byte[] Cost { get; }

    public int Index(int i, int j) => j * Width + i;
    public bool InRange(int i, int j) => i >= 0 && j >= 0 && i < Width && j < Height;
    public bool Walkable(int i, int j) => InRange(i, j) && Mask[Index(i, j)] != 0 && Cost[Index(i, j)] != NavCost.Blocked;

    public (int I, int J) CellOf(float x, float z) => ((int)MathF.Floor((x - OriginX) / Cell), (int)MathF.Floor((z - OriginZ) / Cell));
    public float CenterX(int i) => OriginX + (i + 0.5f) * Cell;
    public float CenterZ(int j) => OriginZ + (j + 0.5f) * Cell;
    public Position Center(int i, int j) => new(CenterX(i), Ground[Index(i, j)], CenterZ(j));

    public (int I, int J)? Nearest(float x, float z, int maxRing = 12)
    {
        (int ci, int cj) = CellOf(x, z);
        (int, int)? best = null;
        float bestDistance = float.MaxValue;
        for (int ring = 0; ring <= maxRing; ring++)
        {
            for (int dj = -ring; dj <= ring; dj++)
            {
                for (int di = -ring; di <= ring; di++)
                {
                    if (Math.Max(Math.Abs(di), Math.Abs(dj)) != ring || !Walkable(ci + di, cj + dj)) continue;
                    float ex = CenterX(ci + di) - x, ez = CenterZ(cj + dj) - z, distance = ex * ex + ez * ez;
                    if (distance < bestDistance) { bestDistance = distance; best = (ci + di, cj + dj); }
                }
            }
            if (best is not null && ring >= 1) break;
        }
        return best;
    }

    public float? HeightAt(float x, float z)
    {
        (int i, int j) = CellOf(x, z);
        return InRange(i, j) && Ground[Index(i, j)] != short.MinValue ? Ground[Index(i, j)] : null;
    }

    public static NavGrid Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 24 || bytes[0] != (byte)'N' || bytes[1] != (byte)'A' || bytes[2] != (byte)'V' || bytes[3] is not ((byte)'1' or (byte)'2'))
            throw new InvalidDataException("To nie jest plik NAV1: " + path);
        var span = bytes.AsSpan();
        float cell = BitConverter.ToSingle(span.Slice(4, 4)), ox = BitConverter.ToSingle(span.Slice(8, 4)), oz = BitConverter.ToSingle(span.Slice(12, 4));
        int w = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(16, 4)), h = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(20, 4));
        int count = w * h, at = 24;
        var ground = new short[count];
        for (int n = 0; n < count; n++) ground[n] = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(at + n * 2, 2));
        at += count * 2;
        byte[] mask = span.Slice(at, count).ToArray();
        at += count;
        byte[] cost = span.Slice(at, count).ToArray();
        at += count;
        short[]? floor = null;
        if (bytes[3] == (byte)'2' && bytes.Length >= at + count * 2)
        {
            floor = new short[count];
            for (int n = 0; n < count; n++) floor[n] = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(at + n * 2, 2));
        }
        return new NavGrid(cell, ox, oz, w, h, ground, mask, cost, floor);
    }
}
