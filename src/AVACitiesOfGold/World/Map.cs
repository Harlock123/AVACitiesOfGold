namespace AVACitiesOfGold.World;

public enum Terrain : byte { Deep, Shallow, Beach, Grass, Forest, Jungle, Desert, Hills, Mountain, Snow, River }

public enum Dir { North, East, South, West }

public static class Dirs
{
    public static (int X, int Y) Step(this Dir d) => d switch
    {
        Dir.North => (0, -1), Dir.East => (1, 0), Dir.South => (0, 1), _ => (-1, 0),
    };
}

/// <summary>A people of the new land: their name, their country, and how they feel about you.</summary>
public sealed class Tribe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public uint Colour { get; set; }
    /// <summary>-100 (at war with you) to 100 (fast friends).</summary>
    public int Attitude { get; set; }
    /// <summary>How readily they fight (warriors per head).</summary>
    public double Boldness { get; set; } = 1;
    public bool Empire { get; set; }
    public int CentreX { get; set; }
    public int CentreY { get; set; }
}

public enum SettlementSize { Village = 1, Town = 2, City = 3, GoldenCity = 4 }

/// <summary>A village, town or city, and what has passed between it and you.</summary>
public sealed class Settlement
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Tribe { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public SettlementSize Size { get; set; }
    public int Gold { get; set; }
    /// <summary>How this settlement's feelings differ from its people's.</summary>
    public int Regard { get; set; }
    public bool Found { get; set; }
    /// <summary>Walked into, not just seen from afar.</summary>
    public bool Visited { get; set; }
    public bool Post { get; set; }
    public bool Ruined { get; set; }
    /// <summary>The day of the last feast here (once a month).</summary>
    public int LastFeast { get; set; } = -999;
    public bool Golden => Size == SettlementSize.GoldenCity;
}

/// <summary>
/// The land itself: a grid of terrain made from the world's seed, the same every time for the same
/// seed (so a saved game only needs the seed).
/// </summary>
public sealed class Map
{
    public const int Width = 128, Height = 80;
    /// <summary>Sail this far east and you are in home waters.</summary>
    public const int HomeWaters = Width - 4;

    private readonly Terrain[] _cells = new Terrain[Width * Height];
    private readonly float[] _height = new float[Width * Height];

    public Terrain this[int x, int y] => x < 0 || y < 0 || x >= Width || y >= Height ? Terrain.Deep : _cells[y * Width + x];
    public float HeightAt(int x, int y) => _height[y * Width + x];

    public static bool IsWater(Terrain t) => t is Terrain.Deep or Terrain.Shallow;
    public bool Water(int x, int y) => IsWater(this[x, y]);
    public bool Land(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && !Water(x, y);

    /// <summary>Days to walk into a square of each kind of ground.</summary>
    public static double Cost(Terrain t) => t switch
    {
        Terrain.Grass or Terrain.Beach => 1, Terrain.Forest or Terrain.Desert or Terrain.River => 1.5,
        Terrain.Jungle or Terrain.Hills or Terrain.Snow => 2, Terrain.Mountain => 3, _ => 0.5,
    };

    public static string Describe(Terrain t) => t switch
    {
        Terrain.Deep => "open sea", Terrain.Shallow => "coastal waters", Terrain.Beach => "shore", Terrain.Grass => "grassland",
        Terrain.Forest => "forest", Terrain.Jungle => "jungle", Terrain.Desert => "desert", Terrain.Hills => "hills",
        Terrain.Mountain => "mountains", Terrain.Snow => "frozen waste", _ => "river",
    };

    // ───────────────────────────── making the land ─────────────────────────────

    private static double Noise(int seed, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        double V(int i, int j)
        {
            uint h = (uint)(i * 374761393 + j * 668265263 + seed * 1442695040);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535.0;
        }
        double a = V(x0, y0) + (V(x0 + 1, y0) - V(x0, y0)) * sx;
        double b = V(x0, y0 + 1) + (V(x0 + 1, y0 + 1) - V(x0, y0 + 1)) * sx;
        return a + (b - a) * sy;
    }

    private static double Fbm(int seed, double x, double y, int octaves = 5)
    {
        double sum = 0, amp = 0.5, freq = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(seed + i * 101, x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5;
            freq *= 2;
        }
        return sum / norm;
    }

    /// <summary>
    /// Makes the new land: one great continent in the west, some islands, open ocean to the east, and
    /// home waters at the eastern edge. Hills and mountains rise toward the far west, rivers run to the
    /// sea, jungle and desert lie to the south, ice to the north.
    /// </summary>
    public static Map Generate(int seed)
    {
        var m = new Map();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                // The continent: an uneven blob spanning the west and middle of the map.
                double cx = (x - 52.0) / 46, cy = (y - 40.0) / 34;
                double blob = 1 - Math.Sqrt(cx * cx + cy * cy);
                double h = blob * 0.75 + Fbm(seed, x / 18.0, y / 18.0) * 0.6 - 0.12;
                // Ridges rise toward the west.
                h += Math.Max(0, (48 - x) / 48.0) * 0.18 * Fbm(seed + 7, x / 9.0, y / 9.0);
                if (x >= Width - 22) h -= 0.6;
                m._height[y * Width + x] = (float)h;
            }
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                double h = m._height[y * Width + x];
                if (h < 0.42) { m._cells[y * Width + x] = Terrain.Deep; continue; }
                double lat = Math.Abs(y - Height / 2.0) / (Height / 2.0);
                double wet = Fbm(seed + 31, x / 14.0, y / 14.0);
                double cold = (y < Height / 2 ? lat * 1.1 : lat * 0.6) + (h - 0.6) * 0.8;
                Terrain t;
                if (h > 0.84) t = cold > 0.55 ? Terrain.Snow : Terrain.Mountain;
                else if (h > 0.72) t = Terrain.Hills;
                else if (cold > 0.78) t = Terrain.Snow;
                else if (y > Height * 0.66 && wet > 0.52) t = Terrain.Jungle;
                else if (y > Height * 0.6 && wet < 0.42) t = Terrain.Desert;
                else if (wet > 0.5) t = Terrain.Forest;
                else t = Terrain.Grass;
                m._cells[y * Width + x] = t;
            }
        // Shallows and beaches along every coast.
        var cells = (Terrain[])m._cells.Clone();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                bool nearLand = false, nearSea = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var n = m[x + dx, y + dy];
                        if (IsWater(n)) nearSea = true; else nearLand = true;
                    }
                var t = m[x, y];
                if (t == Terrain.Deep && nearLand) cells[y * Width + x] = Terrain.Shallow;
                else if (t is Terrain.Grass or Terrain.Forest or Terrain.Desert && nearSea && m._height[y * Width + x] < 0.5) cells[y * Width + x] = Terrain.Beach;
            }
        Array.Copy(cells, m._cells, cells.Length);
        // Rivers: from high ground, downhill to the sea.
        var rng = new Random(seed);
        for (int r = 0; r < 60 && r < 400; r++)
        {
            int x = rng.Next(Width - 24), y = rng.Next(Height);
            if (m[x, y] is not (Terrain.Hills or Terrain.Mountain)) continue;
            for (int step = 0; step < 200; step++)
            {
                var (bx, by) = (x, y);
                float best = m._height[y * Width + x];
                foreach (var d in Enum.GetValues<Dir>())
                {
                    var (dx, dy) = d.Step();
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    if (m._height[ny * Width + nx] < best) { best = m._height[ny * Width + nx]; (bx, by) = (nx, ny); }
                }
                if ((bx, by) == (x, y) || m.Water(bx, by)) break;
                (x, y) = (bx, by);
                if (m[x, y] is not (Terrain.Mountain or Terrain.Snow)) m._cells[y * Width + x] = Terrain.River;
            }
        }
        return m;
    }

    /// <summary>The share of the land in sight from a square: further from hills and mountains.</summary>
    public static int Sight(Terrain t) => t switch { Terrain.Mountain => 6, Terrain.Hills => 5, Terrain.Deep or Terrain.Shallow => 4, _ => 3 };
}
