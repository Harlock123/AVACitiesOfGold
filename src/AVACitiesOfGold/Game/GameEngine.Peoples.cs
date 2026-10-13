using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Game;

public sealed partial class GameEngine
{
    public static string Feeling(int attitude) => attitude switch
    {
        < -50 => "hate you", < -20 => "are hostile", < 10 => "are wary of you", < 40 => "are friendly", < 70 => "welcome you warmly", _ => "treat you as kin",
    };

    public static string SizeName(Settlement s) => s.Size switch
    {
        SettlementSize.Village => "village", SettlementSize.Town => "town", SettlementSize.City => "city", _ => "golden city",
    };

    /// <summary>Walking into a settlement: what its people make of you, and what you can do there.</summary>
    public void Meet(Settlement s)
    {
        var w = World;
        var tribe = w.Tribes[s.Tribe];
        s.Found = true;
        if (!s.Visited)
        {
            s.Visited = true;
            if (s.Golden) Say($"YOU HAVE REACHED {s.Name.ToUpperInvariant()}, A CITY OF GOLD!", 0xFFFFD860);
        }
        if (s.Post)
        {
            Tradepost(s);
            return;
        }
        int att = w.Attitude(s);
        if (att < -30)
        {
            Play(Sfx.Drums);
            var p = new Panel { Title = $"{s.Name}", Kind = PanelKind.Encounter, Place = s };
            p.Say($"Drums beat as you approach the {tribe.Name} {SizeName(s)} of {s.Name}. Warriors come out to meet you, and they are not smiling.");
            p.Add("Offer them gifts (20 goods)", () => { Pop(); Gift(s, 20); Meet(s); }, w.Goods >= 20);
            p.Add("Stand and fight", () => { Pop(); Fight(s); });
            p.Add("Withdraw", () => { Pop(); Withdraw(); });
            Push(p);
            return;
        }
        Show(() =>
        {
            int a = w.Attitude(s);
            var p = new Panel { Title = s.Name, Kind = PanelKind.Encounter, Place = s, Back = Pop };
            string what = s.Golden
                ? $"Here it is: {s.Name}, one of the Seven Cities of Gold. Its walls shine in the sun, and its people {Feeling(a)}."
                : $"The {tribe.Name} {SizeName(s)} of {s.Name}. Its people {Feeling(a)}.";
            p.Say(what);
            p.Say($"You carry {w.Goods} goods. They have {(s.Gold > 200 ? "a great deal of" : s.Gold > 80 ? "some" : s.Gold > 0 ? "a little" : "no")} gold.");
            double rate = Rate(a);
            int goldFor10 = Math.Min(s.Gold, (int)Math.Round(10 * (2 + (int)s.Size) * rate));
            p.Add($"Trade 10 goods for gold (about {goldFor10})", () => TradeGold(s), w.Goods >= 10 && s.Gold > 0);
            p.Add($"Trade 10 goods for food (about {(int)(40 * (0.5 + rate))})", () => TradeFood(s), w.Goods >= 10);
            p.Add("Give them a gift (10 goods)", () => Gift(s, 10), w.Goods >= 10);
            p.Add("Rest and feast with them", () => Feast(s), a >= 20 && w.Day - s.LastFeast >= 30, a < 20 ? "They're not that friendly yet." : "Once a month.");
            p.Add("Ask for guides and news", () => Guides(s), a >= 25, a < 25 ? "They'd need to trust you more." : null);
            p.Add("Found a trading post here (5 men, 20 goods)", () => FoundPost(s), a >= 45 && w.Ashore > 8 && w.Goods >= 20,
                a < 45 ? "They'd need to be good friends first." : "Five men stay to run it. It trades with them for good.");
            p.Add("Demand their gold", () => { Pop(); Fight(s); }, note: "By force. Every people in the land will hear of it.");
            p.Add("Leave", Pop);
            return p;
        });
    }

    /// <summary>How good a price a settlement gives: 0.5 for the wary, 1.5 for kin.</summary>
    private static double Rate(int attitude) => Math.Clamp(0.5 + (attitude + 30) / 100.0, 0.3, 1.5);

    private void TradeGold(Settlement s)
    {
        var w = World;
        int gold = Math.Min(s.Gold, (int)Math.Round(10 * (2 + (int)s.Size) * Rate(w.Attitude(s))));
        w.Goods -= 10;
        s.Gold -= gold;
        w.Gold += gold;
        s.Regard += 2;
        Play(Sfx.Coin);
        Say($"Ten goods for {gold} gold at {s.Name}.", 0xFFFFD860);
    }

    private void TradeFood(Settlement s)
    {
        var w = World;
        int food = (int)(40 * (0.5 + Rate(w.Attitude(s))));
        w.Goods -= 10;
        w.Food += food;
        s.Regard += 2;
        Play(Sfx.Trade);
        Say($"Ten goods for {food} food at {s.Name}.", 0xFF80E080);
    }

    private void Gift(Settlement s, int goods)
    {
        var w = World;
        w.Goods -= goods;
        s.Regard += goods * 3 / 2;
        w.Tribes[s.Tribe].Attitude = Math.Min(100, w.Tribes[s.Tribe].Attitude + goods / 2);
        Play(Sfx.Gift);
        Say($"The people of {s.Name} are pleased with your gifts.", 0xFF80E0FF);
    }

    private void Feast(Settlement s)
    {
        var w = World;
        int food = Math.Max(20, w.Ashore * 3);
        w.Food += food;
        s.LastFeast = (int)w.Day;
        Pass(1);
        Play(Sfx.Feast);
        Say($"A feast at {s.Name}! The men eat well and carry off {food} food.", 0xFF80E080);
    }

    /// <summary>Guides show you the country around, and the elders tell what they know of the golden cities.</summary>
    private void Guides(Settlement s)
    {
        var w = World;
        w.See(s.X, s.Y, 10);
        Play(Sfx.Discover);
        var city = w.Settlements.Where(c => c.Golden && !c.Ruined && !w.HasSeen(c.X, c.Y)).OrderBy(c => Math.Abs(c.X - s.X) + Math.Abs(c.Y - s.Y)).FirstOrDefault();
        string news;
        if (s.Golden) news = "The guides show you the roads between the golden cities.";
        else if (city is null) news = "The guides show you the country for days around. They know nothing more of golden cities that you don't.";
        else
        {
            int dist = Math.Abs(city.X - s.X) + Math.Abs(city.Y - s.Y);
            news = $"The guides show you the country for days around. The elders speak of a city of gold {Direction(city.X - s.X, city.Y - s.Y)}, {(dist < 15 ? "not far" : dist < 35 ? "many days' walk" : "very far")} away.";
            if (w.Attitude(s) >= 60) foreach (var c in w.Settlements.Where(c => c.Golden).Take(2)) w.See(c.X, c.Y, 2);
        }
        Pop();
        Tell(s.Name, news);
    }

    private static string Direction(int dx, int dy)
    {
        double a = Math.Atan2(dy, dx) * 180 / Math.PI;
        string[] names = ["east", "south-east", "south", "south-west", "west", "north-west", "north", "north-east"];
        return "to the " + names[(int)Math.Round(((a + 360) % 360) / 45) % 8];
    }

    private void FoundPost(Settlement s)
    {
        var w = World;
        w.Men -= 5;
        w.Goods -= 20;
        s.Post = true;
        w.Tribes[s.Tribe].Attitude = Math.Min(100, w.Tribes[s.Tribe].Attitude + 15);
        Play(Sfx.Post);
        Pop();
        Tell(s.Name, $"Five of your men stay behind to run a trading post at {s.Name}. The {w.Tribes[s.Tribe].Name} are glad of it: there will be trade, and friends, here from now on.");
    }

    private void Tradepost(Settlement s)
    {
        var w = World;
        int food = Math.Max(20, w.Ashore * 2);
        int gold = Math.Min(60, (int)(w.Day - s.LastFeast) / 4);
        w.Food += food;
        w.Gold += gold;
        s.LastFeast = (int)w.Day;
        Play(Sfx.Post);
        Tell(s.Name, $"Your trading post at {s.Name} is thriving. The keepers hand over {gold} gold and load you up with {food} food.");
    }

    /// <summary>
    /// Force: your men against their warriors. Win, and the gold is yours and the settlement is no more;
    /// lose, and you leave dead behind. Either way every people in the land remembers.
    /// </summary>
    private void Fight(Settlement s)
    {
        var w = World;
        var tribe = w.Tribes[s.Tribe];
        Play(Sfx.Battle);
        double ours = w.Ashore * (1 + w.Rank * 0.1) * (0.7 + Rng.Next(61) / 100.0);
        double theirs = (s.Golden ? 70 : (int)s.Size * 14) * tribe.Boldness * (0.7 + Rng.Next(61) / 100.0);
        tribe.Attitude = Math.Max(-100, tribe.Attitude - 50);
        foreach (var t in w.Tribes.Where(t => t != tribe)) t.Attitude = Math.Max(-100, t.Attitude - 12);
        if (ours > theirs)
        {
            int lost = Math.Min(w.Ashore - 1, (int)Math.Ceiling(theirs * 0.25));
            int gold = s.Gold;
            w.Gold += gold;
            s.Gold = 0;
            s.Ruined = true;
            w.Destroyed++;
            Lose(lost, "battle");
            Tell("Victory", $"Your men overrun {s.Name}. They carry off {gold} gold and leave it in ashes. {lost} of your men die in the fighting. The {tribe.Name} will not forget, nor will their neighbours.");
        }
        else
        {
            int lost = Math.Max(1, (int)Math.Ceiling(w.Ashore * 0.35));
            Lose(lost, "battle");
            if (Mode == Mode.Voyage && !w.Afloat) Withdraw();
            Tell("Defeat", $"The warriors of {s.Name} drive you off. {lost} of your men fall.");
        }
    }

    /// <summary>A war band of an angry people falls on the party.</summary>
    private void Ambush(Tribe tribe)
    {
        var w = World;
        Play(Sfx.Drums);
        double ours = w.Ashore * (0.7 + Rng.Next(61) / 100.0);
        double theirs = (10 + Rng.Next(21)) * tribe.Boldness;
        int lost = ours > theirs ? Math.Max(0, (int)(theirs * 0.15)) : Math.Max(1, (int)(w.Ashore * 0.2));
        int goods = ours > theirs ? 0 : Math.Min(w.Goods, 10 + Rng.Next(11));
        w.Goods -= goods;
        Lose(lost, "battle");
        if (Mode == Mode.Voyage)
            Tell("Ambush!", ours > theirs
                ? $"A {tribe.Name} war band ambushes the party, but is beaten off. {(lost > 0 ? $"{lost} men fall." : "No one is lost.")}"
                : $"A {tribe.Name} war band falls on the party. {lost} men fall and {goods} goods are carried off.");
    }

    /// <summary>Backing away from a settlement to the square you came from.</summary>
    private void Withdraw()
    {
        var w = World;
        foreach (var d in Enum.GetValues<Dir>())
        {
            var (dx, dy) = d.Step();
            int nx = w.X + dx, ny = w.Y + dy;
            if (w.Map.Land(nx, ny) && w.SettlementAt(nx, ny) is null) { w.X = nx; w.Y = ny; break; }
        }
        Say("You withdraw.");
    }
}
