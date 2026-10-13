using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVACitiesOfGold.Game;
using AVACitiesOfGold.Views;
using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set GOLD_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, Helpers.TempSaves());
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("GOLD_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVACitiesOfGold-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the demo pilot explore for a while, to get some of the land charted.</summary>
    private static void Explore(GameEngine g, int steps)
    {
        var pilot = new DemoPilot(g);
        for (int i = 0; i < steps && g.Mode == Mode.Voyage; i++)
        {
            pilot.Update(0.2);
            // Keep the pilot fed and manned, so it explores rather than turning for home.
            g.World.Men = Math.Max(g.World.Men, 40);
            g.World.Goods = Math.Max(g.World.Goods, 40);
            g.World.Food = g.World.Ration * 200;
        }
        g.CloseAll();
    }

    [AvaloniaFact]
    public void The_title_and_the_commission()
    {
        var (w, c, g) = Open();
        g.Update(1);
        Shot(w, c, "title");
        g.Press("Enter");
        Shot(w, c, "commission");
    }

    [AvaloniaFact]
    public void A_voyage()
    {
        var (w, c, g) = Open(21);
        g.NewGame(2024);
        g.Press("Enter");
        for (int i = 0; i < 6; i++) g.Press("Left");
        Shot(w, c, "crossing");
        Explore(g, 1500);
        var world = g.World;
        if (world.Afloat) Explore(g, 400);
        Shot(w, c, "ashore");
        g.ShowMap = true;
        Shot(w, c, "chart");
        g.ShowMap = false;
        // A visit to a town, and one to a city of gold.
        var town = world.Settlements.First(s => s.Size == SettlementSize.Town && !s.Ruined);
        Visit(g, town, 30);
        Shot(w, c, "town");
        g.CloseAll();
        var gold = world.Settlements.First(s => s.Golden);
        Visit(g, gold, 55);
        Shot(w, c, "golden");
    }

    private static void Visit(GameEngine g, Settlement s, int regard)
    {
        var w = g.World;
        foreach (Dir d in Enum.GetValues<Dir>())
        {
            var (dx, dy) = d.Step();
            int x = s.X - dx, y = s.Y - dy;
            if (!w.Map.Land(x, y)) continue;
            w.Afloat = false;
            w.X = x;
            w.Y = y;
            w.See(x, y, 5);
            s.Regard = regard - w.Tribes[s.Tribe].Attitude;
            g.Press(d switch { Dir.North => "Up", Dir.South => "Down", Dir.West => "Left", _ => "Right" });
            return;
        }
    }

    [AvaloniaFact]
    public void The_court_and_the_end()
    {
        var (w, c, g) = Open(31);
        g.NewGame(77);
        g.Press("Enter");
        g.World.Gold = 1800;
        g.World.ShipX = Map.HomeWaters - 1;
        g.Press("Right");
        Shot(w, c, "court");
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label == "Retire");
        g.Press("Enter");
        Shot(w, c, "retired");
    }
}
