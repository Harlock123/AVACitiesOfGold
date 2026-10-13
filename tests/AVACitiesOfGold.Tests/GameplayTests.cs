using AVACitiesOfGold.Game;
using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Tests;

public class GameplayTests
{
    /// <summary>A career under way: the commission read, the ships at sea in home waters.</summary>
    private static GameEngine Voyage(int seed = 5)
    {
        var g = new GameEngine(seed, Helpers.TempSaves());
        g.NewGame(seed);
        g.Press("Enter");
        return g;
    }

    /// <summary>Puts the party ashore beside a settlement (on a land square next to it), ships just offshore.</summary>
    private static Settlement Beside(GameEngine g, Func<Settlement, bool> which, out Dir toward)
    {
        var w = g.World;
        foreach (var s in w.Settlements.Where(which))
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                int x = s.X - dx, y = s.Y - dy;
                if (!w.Map.Land(x, y) || w.SettlementAt(x, y) is not null) continue;
                w.Afloat = false;
                w.X = x;
                w.Y = y;
                w.ShipX = 0;
                w.ShipY = 0;
                toward = d;
                return s;
            }
        throw new InvalidOperationException("no such settlement");
    }

    private static string Key(Dir d) => d switch { Dir.North => "Up", Dir.South => "Down", Dir.West => "Left", _ => "Right" };

    [Fact]
    public void The_same_seed_makes_the_same_world()
    {
        var a = WorldState.New(42);
        var b = WorldState.New(42);
        for (int y = 0; y < Map.Height; y += 3)
            for (int x = 0; x < Map.Width; x += 3)
                Assert.Equal(a.Map[x, y], b.Map[x, y]);
        Assert.Equal(a.Settlements.Select(s => (s.X, s.Y, s.Name)), b.Settlements.Select(s => (s.X, s.Y, s.Name)));
        Assert.NotEqual(WorldState.New(43).Settlements.Select(s => s.Name), a.Settlements.Select(s => s.Name));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void Every_world_has_a_continent_open_sea_home_waters_and_seven_golden_cities(int seed)
    {
        var w = WorldState.New(seed);
        int land = 0;
        for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                if (w.Map.Land(x, y)) land++;
                if (x >= Map.HomeWaters - 18) Assert.True(w.Map.Water(x, y), $"land at {x},{y} near home");
            }
        Assert.InRange(land, 1500, 5000);
        var golden = w.Settlements.Where(s => s.Golden).ToList();
        Assert.Equal(7, golden.Count);
        Assert.All(golden, s => Assert.True(w.Tribes[s.Tribe].Empire));
        Assert.InRange(w.Settlements.Count, 45, 70);
        Assert.All(w.Settlements, s => Assert.True(w.Map.Land(s.X, s.Y)));
        Assert.Equal(6, w.Tribes.Count);
        Assert.Equal(6, w.Settlements.Select(s => s.Tribe).Distinct().Count());
    }

    [Fact]
    public void A_new_career_starts_with_the_crowns_ships_at_sea_in_home_waters()
    {
        var g = new GameEngine(3, Helpers.TempSaves());
        g.Press("Enter");                       // Sail for a new world
        Assert.Equal("The Crown's commission", g.Top!.Title);
        g.Press("Enter");
        Assert.Equal(Mode.Voyage, g.Mode);
        var w = g.World;
        Assert.True(w.Afloat);
        Assert.Equal(2, w.Ships);
        Assert.Equal(30, w.Men);
        Assert.Equal(60, w.Goods);
        Assert.Equal(90, w.DaysOfFood);
        Assert.Equal(1, w.Voyage);
    }

    [Fact]
    public void Sailing_takes_days_and_eats_food()
    {
        var g = Voyage();
        var w = g.World;
        double food = w.Food;
        for (int i = 0; i < 10; i++) g.Press("Left");
        Assert.Equal(Map.HomeWaters - 11, w.ShipX);
        Assert.Equal(10 * GameEngine.SeaDay, w.Day, 1);
        Assert.True(w.Food < food);
    }

    [Fact]
    public void Going_ashore_leaves_keepers_aboard_and_walking_back_boards_the_ships()
    {
        var g = Voyage();
        var w = g.World;
        // Find a coast square with sea to its east.
        for (int y = 0; y < Map.Height; y++)
            for (int x = Map.HomeWaters - 1; x > 0; x--)
                if (w.Map.Land(x, y) && w.Map.Water(x + 1, y) && w.SettlementAt(x, y) is null)
                {
                    w.ShipX = x + 1;
                    w.ShipY = y;
                    g.Press("Left");
                    Assert.False(w.Afloat);
                    Assert.Equal((x, y), (w.X, w.Y));
                    Assert.Equal(w.Ships * 3, w.Keepers);
                    Assert.Equal(w.Men - w.Keepers, w.Ashore);
                    g.CloseAll();
                    g.Press("Right");
                    Assert.True(w.Afloat);
                    return;
                }
        Assert.Fail("no coast");
    }

    [Fact]
    public void Without_food_men_starve()
    {
        var g = Voyage();
        var w = g.World;
        w.Food = 0;
        int men = w.Men;
        for (int i = 0; i < 6; i++) { g.CloseAll(); g.Press("Left"); }
        Assert.True(w.Men < men);
        Assert.True(w.MenLost > 0);
    }

    [Fact]
    public void Trading_giving_and_guides_at_a_friendly_settlement()
    {
        var g = Voyage();
        var w = g.World;
        var s = Beside(g, s => !s.Golden && s.Size == SettlementSize.Town, out var dir);
        s.Regard = 60 - w.Tribes[s.Tribe].Attitude;
        g.Press(Key(dir));
        Assert.Equal(PanelKind.Encounter, g.Top!.Kind);
        Assert.Same(s, g.Top.Place);
        int goods = w.Goods, gold = w.Gold;
        g.Press("1");                           // trade for gold
        Assert.Equal(goods - 10, w.Goods);
        Assert.True(w.Gold > gold);
        int before = w.Attitude(s);
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.StartsWith("Give"));
        g.Press("Enter");
        Assert.True(w.Attitude(s) > before);
        int explored = w.Explored;
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.StartsWith("Ask"));
        g.Press("Enter");
        Assert.True(w.Explored > explored);
    }

    [Fact]
    public void A_trading_post_needs_good_friends_and_pays_at_home()
    {
        var g = Voyage();
        var w = g.World;
        var s = Beside(g, s => !s.Golden, out var dir);
        s.Regard = 80 - w.Tribes[s.Tribe].Attitude;
        g.Press(Key(dir));
        int men = w.Men;
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.StartsWith("Found"));
        g.Press("Enter");
        Assert.True(s.Post);
        Assert.Equal(men - 5, w.Men);
        Assert.Equal(1, w.Posts);
        // Home: the post sends its share.
        g.CloseAll();
        w.Afloat = true;
        w.ShipX = Map.HomeWaters - 1;
        w.ShipY = 40;
        w.Gold = 0;
        g.Press("Right");
        Assert.Equal(Mode.Court, g.Mode);
        Assert.Equal(40, w.GoldDelivered);
    }

    [Fact]
    public void A_hostile_people_meets_you_with_drums()
    {
        var g = Voyage();
        var w = g.World;
        var s = Beside(g, s => !s.Golden, out var dir);
        w.Tribes[s.Tribe].Attitude = -80;
        s.Regard = 0;
        g.Press(Key(dir));
        Assert.Contains(g.Top!.Options, o => o.Label == "Stand and fight");
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label == "Withdraw");
        g.Press("Enter");
        Assert.NotEqual((s.X, s.Y), (w.X, w.Y));
    }

    [Fact]
    public void Force_wins_gold_but_every_people_remembers()
    {
        var g = Voyage();
        var w = g.World;
        var s = Beside(g, s => s.Size == SettlementSize.Village && !w.Tribes[s.Tribe].Empire, out var dir);
        w.Men = 200;
        int gold = s.Gold;
        var others = w.Tribes.Where(t => t.Id != s.Tribe).Select(t => t.Attitude).ToList();
        g.Press(Key(dir));
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.StartsWith("Demand"));
        g.Press("Enter");
        Assert.True(s.Ruined);
        Assert.Equal(gold, w.Gold);
        Assert.Equal(1, w.Destroyed);
        Assert.All(w.Tribes.Where(t => t.Id != s.Tribe).Zip(others), p => Assert.True(p.First.Attitude < p.Second));
        Assert.Null(w.SettlementAt(s.X, s.Y));
    }

    [Fact]
    public void Reaching_a_golden_city_counts()
    {
        var g = Voyage();
        var w = g.World;
        var s = Beside(g, s => s.Golden, out var dir);
        s.Regard = 50 - w.Tribes[s.Tribe].Attitude;
        Assert.Equal(0, w.CitiesFound);
        g.Press(Key(dir));
        Assert.Equal(1, w.CitiesFound);
        Assert.Contains("Cities of Gold", string.Join(" ", g.Top!.Text));
    }

    [Fact]
    public void Home_with_gold_brings_renown_and_promotion_and_a_bigger_voyage()
    {
        var g = Voyage();
        var w = g.World;
        w.Gold = 2000;
        w.ShipX = Map.HomeWaters - 1;
        g.Press("Right");
        Assert.Equal(Mode.Court, g.Mode);
        Assert.Equal(2000, w.GoldDelivered);
        Assert.Equal(1, w.Rank);
        Assert.Contains(g.Top!.Text, t => t.Contains("COMMODORE"));
        g.Press("1");                           // set sail
        Assert.Equal(Mode.Voyage, g.Mode);
        Assert.Equal(3, w.Ships);
        Assert.Equal(45, w.Men);
        Assert.Equal(2, w.Voyage);
    }

    [Fact]
    public void A_lost_expedition_comes_back_to_court_with_nothing()
    {
        var g = Voyage();
        var w = g.World;
        w.Gold = 500;
        w.Men = 1;
        w.Food = 0;
        for (int i = 0; i < 10 && g.Mode == Mode.Voyage; i++)
        {
            if (g.Top is not null) g.Press("Enter");
            else g.Press("Left");
        }
        while (g.Top is { Kind: not PanelKind.Court }) g.Press("Enter");
        Assert.Equal(Mode.Court, g.Mode);
        Assert.Equal(0, w.GoldDelivered);
    }

    [Fact]
    public void Retiring_ends_the_career_with_a_reckoning()
    {
        var g = Voyage();
        var w = g.World;
        w.ShipX = Map.HomeWaters - 1;
        g.Press("Right");
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label == "Retire");
        g.Press("Enter");
        Assert.Equal(Mode.Ended, g.Mode);
        Assert.Contains(g.Top!.Text, t => t.StartsWith("RENOWN"));
    }

    [Fact]
    public void After_twenty_years_the_crown_retires_you()
    {
        var g = Voyage();
        var w = g.World;
        w.Day = 360 * 21;
        w.ShipX = Map.HomeWaters - 1;
        g.Press("Right");
        Assert.DoesNotContain(g.Top!.Options, o => o.Label.StartsWith("Set sail"));
    }

    [Fact]
    public void Saving_and_loading_bring_back_the_whole_career()
    {
        var g = Voyage();
        var w = g.World;
        for (int i = 0; i < 8; i++) g.Press("Left");
        w.Gold = 77;
        w.Settlements[3].Regard = 25;
        w.Settlements[5].Found = true;
        g.SaveMenu();
        g.SaveTo(2);
        string before = w.ToJson();
        g.World.Gold = 0;
        Assert.True(g.LoadFrom(2));
        Assert.Equal(Mode.Voyage, g.Mode);
        Assert.Equal(77, g.World.Gold);
        Assert.Equal(25, g.World.Settlements[3].Regard);
        Assert.True(g.World.Settlements[5].Found);
        Assert.Equal(w.Map[30, 30], g.World.Map[30, 30]);
        Assert.Equal(before.Replace(w.Saved.ToString("O"), ""), g.World.ToJson().Replace(g.World.Saved.ToString("O"), ""));
    }

    [Fact]
    public void The_title_turns_into_a_demonstration_that_sails_by_itself()
    {
        var saves = Helpers.TempSaves();
        var g = new GameEngine(5, saves);
        for (int i = 0; i < 31 * 10; i++) g.Update(0.1);
        Assert.True(g.InDemo);
        var start = g.Here;
        for (int i = 0; i < 20 * 10; i++) g.Update(0.1);
        Assert.NotEqual(start, g.Here);
        g.Press("Space");
        Assert.False(g.InDemo);
        Assert.Equal(Mode.Title, g.Mode);
        Assert.False(saves.Any());
    }

    [Fact]
    public void The_demo_pilot_moves_on_after_meeting_people_rather_than_lingering()
    {
        var g = new GameEngine(21, Helpers.TempSaves());
        g.NewGame(2024);
        g.Press("Enter");
        var pilot = new DemoPilot(g);
        for (int i = 0; i < 1500 && g.Mode == Mode.Voyage; i++)
        {
            pilot.Update(0.2);
            // Plenty of everything, so only a stuck pilot would stop exploring.
            g.World.Men = Math.Max(g.World.Men, 40);
            g.World.Goods = Math.Max(g.World.Goods, 40);
            g.World.Food = g.World.Ration * 200;
        }
        Assert.True(g.World.Explored >= 5, $"only {g.World.Explored}% charted");
    }

    [Fact]
    public void Every_sound_has_a_file()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(dir, s + ".wav")), s.ToString());
    }
}
