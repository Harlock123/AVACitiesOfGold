namespace AVACitiesOfGold.World;

/// <summary>
/// The game's only source of chance: a small xorshift generator whose whole state is one number, so
/// a saved game carries it and carries on exactly where it left off.
/// </summary>
public sealed class Rng
{
    public ulong State { get; set; }

    public Rng(ulong seed) => State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public ulong NextRaw()
    {
        var x = State;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        State = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>0 ≤ n &lt; max.</summary>
    public int Next(int max) => max <= 1 ? 0 : (int)(NextRaw() % (ulong)max);

    /// <summary>lo ≤ n ≤ hi.</summary>
    public int Range(int lo, int hi) => hi <= lo ? lo : lo + Next(hi - lo + 1);

    /// <summary>One die: 1 to sides.</summary>
    public int D(int sides) => 1 + Next(sides);

    public bool Chance(int percent) => Next(100) < percent;

    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];
}

/// <summary>A roll such as 2d6+1.</summary>
public readonly record struct Dice(int Count, int Sides, int Plus = 0)
{
    public int Roll(Rng rng)
    {
        int total = Plus;
        for (int i = 0; i < Count; i++) total += rng.D(Sides);
        return total;
    }

    public int Max => Count * Sides + Plus;
    public int Min => Count + Plus;
    public double Average => Count * (Sides + 1) / 2.0 + Plus;

    public static Dice Parse(string s)
    {
        int d = s.IndexOf('d');
        int sign = s.IndexOfAny(['+', '-'], Math.Max(0, d));
        int count = d <= 0 ? 1 : int.Parse(s[..d]);
        if (d < 0) return new Dice(0, 1, int.Parse(s));
        int sides = int.Parse(sign < 0 ? s[(d + 1)..] : s[(d + 1)..sign]);
        int plus = sign < 0 ? 0 : int.Parse(s[sign..]);
        return new Dice(count, sides, plus);
    }

    public override string ToString() =>
        Count == 0 ? Plus.ToString() : $"{Count}d{Sides}{(Plus > 0 ? "+" + Plus : Plus < 0 ? Plus.ToString() : "")}";
}
