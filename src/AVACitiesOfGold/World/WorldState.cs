using System.Text.Json;
using System.Text.Json.Serialization;

namespace AVACitiesOfGold.World;

/// <summary>Everything a saved game holds. The land itself comes back from its seed.</summary>
public sealed class WorldState
{
    public int Seed { get; set; }
    public List<Tribe> Tribes { get; set; } = [];
    public List<Settlement> Settlements { get; set; } = [];
    /// <summary>Squares seen: one character per square.</summary>
    public string Seen { get; set; } = new('0', Map.Width * Map.Height);

    // The expedition.
    public bool Afloat { get; set; } = true;
    public int ShipX { get; set; }
    public int ShipY { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Ships { get; set; }
    public int Men { get; set; }
    public double Food { get; set; }
    public int Goods { get; set; }
    public int Gold { get; set; }
    public double Day { get; set; }
    public bool AtHome { get; set; } = true;

    // The career.
    public int Voyage { get; set; }
    public int GoldDelivered { get; set; }
    public int Destroyed { get; set; }
    public int MenLost { get; set; }
    public bool Retired { get; set; }
    public DateTime Saved { get; set; }

    [JsonIgnore] private Map? _map;
    [JsonIgnore] public Map Map => _map ??= Map.Generate(Seed);

    public int Year => (int)(Day / 360) + 1;
    public string Date => $"{Months[(int)(Day % 360) / 30]} {(int)(Day % 30) + 1}, YEAR {Year}";
    private static readonly string[] Months = ["FIRSTFROST", "DEEPSNOW", "THAW", "SEEDING", "BLOSSOM", "LONGDAY", "HIGHSUN", "HARVEST", "GOLDLEAF", "FALLING", "STORMS", "YEAR'S END"];

    /// <summary>The men a voyage leaves aboard to keep the ships while the rest go ashore.</summary>
    public int Keepers => Math.Min(Men, Ships * 3);
    public int Ashore => Afloat ? 0 : Math.Max(0, Men - Keepers);
    /// <summary>Food eaten a day by everyone on the voyage.</summary>
    public double Ration => Math.Max(1, Men / 10.0);
    public int DaysOfFood => (int)(Food / Ration);

    public bool HasSeen(int x, int y) => x >= 0 && y >= 0 && x < Map.Width && y < Map.Height && Seen[y * Map.Width + x] == '1';

    public void See(int cx, int cy, int radius)
    {
        var chars = Seen.ToCharArray();
        for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if (x >= 0 && y >= 0 && x < Map.Width && y < Map.Height && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius + 1)
                    chars[y * Map.Width + x] = '1';
        Seen = new string(chars);
        foreach (var s in Settlements.Where(s => !s.Found && HasSeen(s.X, s.Y))) s.Found = true;
    }

    /// <summary>The share of the land that has been seen (0–100).</summary>
    public int Explored
    {
        get
        {
            int land = 0, seen = 0;
            for (int y = 0; y < Map.Height; y++)
                for (int x = 0; x < Map.Width; x++)
                    if (Map.Land(x, y)) { land++; if (Seen[y * Map.Width + x] == '1') seen++; }
            return land == 0 ? 0 : seen * 100 / land;
        }
    }

    public Settlement? SettlementAt(int x, int y) => Settlements.FirstOrDefault(s => s.X == x && s.Y == y && !s.Ruined);

    /// <summary>How a settlement feels about you: its people's feeling, plus its own.</summary>
    public int Attitude(Settlement s) => s.Post ? 100 : Math.Clamp(Tribes[s.Tribe].Attitude + s.Regard, -100, 100);

    public int Posts => Settlements.Count(s => s.Post);
    public int CitiesFound => Settlements.Count(s => s.Golden && s.Visited);
    public int Found => Settlements.Count(s => s.Found);

    /// <summary>Renown: gold brought home, discoveries, trading posts, and the ruin you have caused.</summary>
    public int Score => GoldDelivered + 300 * CitiesFound + 40 * Found + 250 * Posts + 15 * Explored - 200 * Destroyed;

    public static readonly string[] Ranks = ["CAPTAIN", "COMMODORE", "ADMIRAL", "GOVERNOR", "VICEROY"];
    public static readonly int[] RankScore = [0, 1500, 4000, 8000, 14000];
    public int Rank => RankScore.Count(r => Score >= r) - 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static WorldState FromJson(string json) => JsonSerializer.Deserialize<WorldState>(json, Options) ?? throw new InvalidDataException("Not a saved game.");

    /// <summary>A whole new world from a seed: land, peoples and settlements.</summary>
    public static WorldState New(int seed)
    {
        var w = new WorldState { Seed = seed };
        var rng = new Random(seed);
        w.Tribes = Peoples.MakeTribes(w.Map, rng);
        w.Settlements = Peoples.MakeSettlements(w.Map, w.Tribes, rng);
        return w;
    }
}
