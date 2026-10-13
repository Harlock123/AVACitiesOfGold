namespace AVACitiesOfGold.World;

/// <summary>
/// The peoples of the new land and where they live. Every name here is invented, and the land is
/// made fresh from the seed: it is nobody's real history.
/// </summary>
public static class Peoples
{
    private static readonly (string Name, string Country, uint Colour, string[] Syllables, double Boldness)[] Kinds =
    [
        ("Ahnaru", "the Dawn Coast", 0xFF40A0C0, ["ah", "na", "ru", "ko", "ma", "li", "sa", "te"], 0.8),
        ("Tecolo", "the Cedar Lands", 0xFF40A060, ["te", "co", "lo", "wan", "ki", "sho", "ra", "mi"], 1.0),
        ("Ruuma", "the Long Grass", 0xFFC0A040, ["ru", "um", "ba", "ho", "ta", "ne", "ka", "lo"], 1.2),
        ("Ilvani", "the Green Deep", 0xFF60C060, ["il", "va", "ni", "qu", "e", "tza", "lo", "mo"], 0.9),
        ("Kesh", "the Red Mesas", 0xFFD07040, ["ke", "sh", "ta", "ha", "ri", "zo", "an", "du"], 1.3),
        ("Sunward Empire", "the Sunward Heights", 0xFFE0C040, ["in", "ti", "ca", "ya", "ol", "lan", "cu", "ra"], 1.6),
    ];

    /// <summary>The peoples, with their homelands spread across the continent.</summary>
    public static List<Tribe> MakeTribes(Map map, Random rng)
    {
        int minX = Map.Width, maxX = 0;
        for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
                if (map.Land(x, y)) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); }
        int w = maxX - minX;
        (double X, double Y)[] spots =
        [
            (maxX - w * 0.1, Map.Height * 0.5), (minX + w * 0.55, Map.Height * 0.22), (minX + w * 0.5, Map.Height * 0.5),
            (minX + w * 0.65, Map.Height * 0.8), (minX + w * 0.3, Map.Height * 0.72), (minX + w * 0.12, Map.Height * 0.38),
        ];
        var tribes = new List<Tribe>();
        for (int i = 0; i < Kinds.Length; i++)
        {
            var (x, y) = Nearest(map, (int)spots[i].X, (int)spots[i].Y);
            tribes.Add(new Tribe
            {
                Id = i, Name = Kinds[i].Name, Country = Kinds[i].Country, Colour = Kinds[i].Colour, Boldness = Kinds[i].Boldness,
                Empire = i == Kinds.Length - 1, CentreX = x, CentreY = y, Attitude = rng.Next(-5, 21),
            });
        }
        return tribes;
    }

    private static (int, int) Nearest(Map map, int x, int y)
    {
        for (int r = 0; r < 60; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (map.Land(x + dx, y + dy) && map[x + dx, y + dy] is not (Terrain.Mountain or Terrain.Snow)) return (x + dx, y + dy);
        return (x, y);
    }

    /// <summary>Whose land a square is: the nearest people's, with ragged borders.</summary>
    public static int Owner(List<Tribe> tribes, int x, int y)
    {
        int best = 0;
        double bestD = double.MaxValue;
        foreach (var t in tribes)
        {
            double jitter = ((x * 31 + y * 17 + t.Id * 13) % 7) * 0.6;
            double d = Math.Sqrt((t.CentreX - x) * (t.CentreX - x) + (t.CentreY - y) * (t.CentreY - y)) + jitter;
            if (d < bestD) { bestD = d; best = t.Id; }
        }
        return best;
    }

    public static string NameFor(int tribe, Random rng)
    {
        var syl = Kinds[tribe].Syllables;
        string s = "";
        int n = 2 + rng.Next(2);
        for (int i = 0; i < n; i++) s += syl[rng.Next(syl.Length)];
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>
    /// Settlements: villages, towns and cities where people would live (by rivers and coasts, on good
    /// ground), and the seven golden cities of the Sunward Empire high in the western hills.
    /// </summary>
    public static List<Settlement> MakeSettlements(Map map, List<Tribe> tribes, Random rng)
    {
        var list = new List<Settlement>();
        var empire = tribes.First(t => t.Empire);
        bool Spaced(int x, int y, int gap) => list.All(s => Math.Max(Math.Abs(s.X - x), Math.Abs(s.Y - y)) >= gap);
        var candidates = new List<(int X, int Y, double Score)>();
        for (int y = 2; y < Map.Height - 2; y++)
            for (int x = 2; x < Map.HomeWaters - 10; x++)
            {
                var t = map[x, y];
                if (!map.Land(x, y) || t is Terrain.Mountain or Terrain.Snow or Terrain.River) continue;
                double score = t switch { Terrain.Grass => 3, Terrain.Forest => 2.5, Terrain.Beach => 2.5, Terrain.Hills => 2, Terrain.Jungle => 1.5, _ => 1 };
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (map[x + dx, y + dy] == Terrain.River) score += 1.5;
                        if (map.Water(x + dx, y + dy)) score += 0.8;
                    }
                candidates.Add((x, y, score + rng.NextDouble() * 3));
            }
        // The golden cities first, in the empire's hills and high grass.
        foreach (var (x, y, _) in candidates.Where(c => Owner(tribes, c.X, c.Y) == empire.Id && map[c.X, c.Y] is Terrain.Hills or Terrain.Grass)
                     .OrderBy(c => Math.Abs(c.X - empire.CentreX) + Math.Abs(c.Y - empire.CentreY) + rng.Next(14)))
        {
            if (list.Count(s => s.Golden) >= 7) break;
            if (!Spaced(x, y, 4)) continue;
            list.Add(new Settlement { Id = list.Count, Tribe = empire.Id, X = x, Y = y, Size = SettlementSize.GoldenCity, Gold = 600 + rng.Next(601), Name = NameFor(empire.Id, rng) });
        }
        foreach (var (x, y, _) in candidates.OrderByDescending(c => c.Score))
        {
            if (list.Count >= 62) break;
            if (!Spaced(x, y, 5)) continue;
            int tribe = Owner(tribes, x, y);
            var size = rng.Next(10) switch { < 6 => SettlementSize.Village, < 9 => SettlementSize.Town, _ => SettlementSize.City };
            int gold = size switch { SettlementSize.Village => 20 + rng.Next(41), SettlementSize.Town => 60 + rng.Next(91), _ => 150 + rng.Next(151) };
            if (tribes[tribe].Empire) gold *= 2;
            list.Add(new Settlement { Id = list.Count, Tribe = tribe, X = x, Y = y, Size = size, Gold = gold, Name = NameFor(tribe, rng) });
        }
        return list;
    }
}
