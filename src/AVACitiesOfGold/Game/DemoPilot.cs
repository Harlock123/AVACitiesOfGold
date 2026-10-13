using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Game;

/// <summary>
/// Plays the attract demo: sail west to the nearest unseen coast, go ashore, walk toward land not yet
/// seen, and deal kindly with whoever lives there.
/// </summary>
public sealed class DemoPilot(GameEngine game)
{
    private double _next = 0.8;
    public double Elapsed { get; private set; }
    private readonly HashSet<(int, int)> _been = [];
    private readonly HashSet<(string, string)> _done = [];

    public void Update(double dt)
    {
        Elapsed += dt;
        _next -= dt;
        if (_next > 0) return;
        _next = 0.12;
        if (game.Top is { } p)
        {
            // Prefer trading and friendship; never force.
            // Each kindness once per place, then move on, so the demo never lingers in one village.
            int pick = p.Options.FindIndex(o => o.Enabled && o.Act is not null && !_done.Contains((p.Title, o.Label)) && (o.Label.StartsWith("Ask") || o.Label.StartsWith("Trade 10 goods for gold") || o.Label.StartsWith("Give")));
            if (pick >= 0) _done.Add((p.Title, p.Options[pick].Label));
            if (pick < 0) pick = p.Options.FindIndex(o => o.Enabled && o.Act is not null && (o.Label.StartsWith("Leave") || o.Label.StartsWith("Continue") || o.Label.StartsWith("Withdraw")));
            if (pick < 0) pick = p.Options.FindIndex(o => o.Enabled && o.Act is not null && !o.Label.StartsWith("Demand") && !o.Label.StartsWith("Stand"));
            if (pick >= 0) { p.Selected = pick; game.Input("Enter"); } else game.Input("Esc");
            _next = 0.9;
            return;
        }
        var w = game.World;
        _been.Add(game.Here);
        // Enough food left to get home? If not, back to the ships and east.
        double homeDays = (Map.HomeWaters - w.ShipX) * GameEngine.SeaDay + (w.Afloat ? 0 : (Math.Abs(w.X - w.ShipX) + Math.Abs(w.Y - w.ShipY)) * 1.6) + 6;
        if (w.DaysOfFood < homeDays || w.Men < 12) _homeward = true;
        var step = _homeward ? Homeward() : w.Afloat ? ToShore() : NextStep();
        if (step is not { } to) { game.Input(w.Afloat ? "Left" : "F"); return; }
        var (x, y) = game.Here;
        game.Input(to.X > x ? "Right" : to.X < x ? "Left" : to.Y > y ? "Down" : "Up");
    }

    /// <summary>At sea: sail west until land is in sight, then make for the nearest shore and step onto it.</summary>
    private (int X, int Y)? ToShore()
    {
        var w = game.World;
        var map = w.Map;
        var start = game.Here;
        var prev = new Dictionary<(int, int), (int, int)> { [start] = start };
        var q = new Queue<(int, int)>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                var n = (x + dx, y + dy);
                if (prev.ContainsKey(n) || n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= Map.Width || n.Item2 >= Map.Height || n.Item1 >= Map.HomeWaters) continue;
                if (!w.HasSeen(n.Item1, n.Item2)) continue;
                prev[n] = (x, y);
                if (map.Land(n.Item1, n.Item2) && map[n.Item1, n.Item2] is not (Terrain.Mountain or Terrain.Snow) && !_landed.Contains(n))
                {
                    _landed.Add(n);
                    var at = n;
                    while (prev[at] != start) at = prev[at];
                    return at;
                }
                if (map.Water(n.Item1, n.Item2)) q.Enqueue(n);
            }
        }
        return (start.X - 1, start.Y);
    }

    private readonly HashSet<(int, int)> _landed = [];
    private bool _homeward;

    /// <summary>Back to the ships (ashore), or east toward home waters (afloat).</summary>
    private (int X, int Y)? Homeward()
    {
        var w = game.World;
        var map = w.Map;
        var start = game.Here;
        (int, int) goal = w.Afloat ? (Map.HomeWaters, start.Y) : (w.ShipX, w.ShipY);
        var prev = new Dictionary<(int, int), (int, int)> { [start] = start };
        var q = new Queue<(int, int)>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                var n = (x + dx, y + dy);
                if (prev.ContainsKey(n) || n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= Map.Width || n.Item2 >= Map.Height) continue;
                bool water = map.Water(n.Item1, n.Item2);
                bool reached = w.Afloat ? water && n.Item1 >= Map.HomeWaters : n == goal;
                if (!reached && (w.Afloat ? !water : water)) continue;
                prev[n] = (x, y);
                if (reached)
                {
                    var at = n;
                    while (prev[at] != start) at = prev[at];
                    return at;
                }
                q.Enqueue(n);
            }
        }
        return (start.X + 1, start.Y);
    }

    /// <summary>The first step toward the nearest square never stood on next to unseen country.</summary>
    private (int X, int Y)? NextStep()
    {
        var w = game.World;
        var map = w.Map;
        var start = game.Here;
        var prev = new Dictionary<(int, int), (int, int)> { [start] = start };
        var q = new Queue<(int, int)>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                var n = (x + dx, y + dy);
                if (prev.ContainsKey(n) || n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= Map.Width || n.Item2 >= Map.Height) continue;
                bool water = map.Water(n.Item1, n.Item2);
                // Afloat, sail the sea and step ashore; ashore, keep to the land.
                if (w.Afloat ? (!water && !(x, y).Equals(start)) : water) continue;
                if (!water && map[n.Item1, n.Item2] is Terrain.Mountain or Terrain.Snow) continue;
                if (n.Item1 >= Map.HomeWaters) continue;
                prev[n] = (x, y);
                bool fresh = !_been.Contains(n) && Unseen(n.Item1, n.Item2);
                if (fresh || (!water && w.Afloat))
                {
                    var at = n;
                    while (prev[at] != start) at = prev[at];
                    return at;
                }
                if (!water || w.Afloat) q.Enqueue(n);
            }
        }
        return null;
    }

    private bool Unseen(int x, int y)
    {
        var w = game.World;
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                if (x + dx >= 0 && y + dy >= 0 && x + dx < Map.Width && y + dy < Map.Height && !w.HasSeen(x + dx, y + dy)) return true;
        return false;
    }
}
