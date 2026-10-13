using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Game;

public sealed partial class GameEngine
{
    public const double SeaDay = 0.4;

    /// <summary>Where the expedition is: the ships at sea, or the party ashore.</summary>
    public (int X, int Y) Here => World.Afloat ? (World.ShipX, World.ShipY) : (World.X, World.Y);

    private int SightHere => Map.Sight(World.Map[Here.X, Here.Y]);

    /// <summary>A step: the ships sail, or the party walks; walking off the shore boards the ships.</summary>
    public void Move(Dir d)
    {
        var w = World;
        var map = w.Map;
        var (dx, dy) = d.Step();
        if (w.Afloat)
        {
            int nx = w.ShipX + dx, ny = w.ShipY + dy;
            if (nx < 0 || ny < 0 || nx >= Map.Width || ny >= Map.Height) { Bump(); return; }
            if (map.Water(nx, ny))
            {
                w.ShipX = nx;
                w.ShipY = ny;
                Play(Sfx.Sail);
                Pass(SeaDay);
                w.See(nx, ny, SightHere);
                if (nx >= Map.HomeWaters) { ComeHome(); return; }
                return;
            }
            // Land ahead: go ashore.
            if (w.Men - w.Keepers < 1) { Say("There's nobody to spare to go ashore."); Bump(); return; }
            w.Afloat = false;
            w.X = nx;
            w.Y = ny;
            Play(Sfx.Ashore);
            Play(Sfx.Land);
            Say($"{w.Ashore} men wade ashore. {w.Keepers} stay to keep the ships.", 0xFFA0E0FF);
            Arrive();
            return;
        }
        int lx = w.X + dx, ly = w.Y + dy;
        if ((lx, ly) == (w.ShipX, w.ShipY))
        {
            w.Afloat = true;
            Play(Sfx.Sail);
            Play(Sfx.Sea);
            Say("The party rows back out to the ships.", 0xFFA0E0FF);
            return;
        }
        if (!map.Land(lx, ly)) { Say("Only open water that way."); Bump(); return; }
        w.X = lx;
        w.Y = ly;
        Play(Sfx.Step);
        Pass(Map.Cost(map[lx, ly]));
        if (Mode != Mode.Voyage || w.Afloat) return;
        Arrive();
    }

    private void Bump()
    {
        Play(Sfx.Bump);
        Bumped = 0.12;
    }

    /// <summary>On reaching a square ashore: look around, and meet whoever lives there.</summary>
    private void Arrive()
    {
        var w = World;
        int before = w.Found;
        w.See(w.X, w.Y, SightHere);
        if (w.Found > before) Play(Sfx.Discover);
        if (w.SettlementAt(w.X, w.Y) is { } s) Meet(s);
    }

    /// <summary>The day ticks over: food is eaten, and the land or sea may have something to say.</summary>
    public void Pass(double days)
    {
        var w = World;
        int before = (int)w.Day;
        w.Day += days;
        double food = w.Ration * days * (w.Afloat || w.Map[w.X, w.Y] != Terrain.Desert ? 1 : 1.6);
        w.Food -= food;
        if (w.Food < 0)
        {
            w.Food = 0;
            int lost = Math.Max(1, (int)Math.Ceiling(w.Men * 0.06 * days));
            Lose(lost, "starve");
        }
        for (int day = before + 1; day <= (int)w.Day && Mode == Mode.Voyage && Top is null; day++)
        {
            if (w.Afloat) SeaDayEvents();
            else LandDayEvents();
        }
    }

    private void Lose(int men, string how)
    {
        var w = World;
        men = Math.Min(men, w.Men);
        if (men <= 0) return;
        w.Men -= men;
        w.MenLost += men;
        Say(how switch
        {
            "starve" => $"With no food, {men} {Men(men)} starve.",
            "fall" => $"{men} {Men(men)} lost in the high passes.",
            "fever" => $"Fever takes {men} {Men(men)}.",
            _ => $"{men} {Men(men)} lost.",
        }, 0xFFFF8060);
        Play(Sfx.Loss);
        if (w.Men <= 0) Lost();
        else if (!w.Afloat && w.Ashore <= 0)
        {
            // The shore party is gone; the keepers on the ships are all that's left.
            w.Afloat = true;
            Say("The last of the shore party is lost. The ships wait offshore.", 0xFFFF8060);
        }
    }

    private static string Men(int n) => n == 1 ? "man is" : "men are";

    private void SeaDayEvents()
    {
        var w = World;
        if (Rng.Next(1000) < 12)
        {
            Play(Sfx.Storm);
            if (w.Ships > 1 && Rng.Next(100) < 45)
            {
                int men = w.Men / w.Ships;
                w.Ships--;
                w.Men -= men;
                w.MenLost += men;
                Tell("A storm!", $"A storm blows up out of nowhere. When it clears, one ship is gone, and {men} men with her.");
            }
            else
            {
                int food = (int)(w.Food * 0.2);
                w.Food -= food;
                Tell("A storm!", $"A storm batters the ships. Waves spoil {food} days' worth of stores.");
            }
        }
    }

    private void LandDayEvents()
    {
        var w = World;
        var t = w.Map[w.X, w.Y];
        var tribe = w.Tribes[Peoples.Owner(w.Tribes, w.X, w.Y)];
        int roll = Rng.Next(1000);
        if (t is Terrain.Grass or Terrain.Forest or Terrain.Jungle or Terrain.River && roll < 160)
        {
            int food = Math.Max(2, (int)(w.Ashore * 0.7));
            w.Food += food;
            Say($"The men hunt and gather: +{food} food.", 0xFF80E080);
            Play(Sfx.Forage);
        }
        else if (t is Terrain.Mountain or Terrain.Snow && roll >= 950) Lose(1 + Rng.Next(2), "fall");
        else if (t == Terrain.Jungle && roll >= 970) Lose(1, "fever");
        else if (tribe.Attitude < -25 && roll >= 930) Ambush(tribe);
        else if (roll >= 995)
        {
            int gold = 20 + Rng.Next(61);
            w.Gold += gold;
            Say($"In an overgrown ruin the men find {gold} gold.", 0xFFFFD860);
            Play(Sfx.Coin);
        }
    }

    /// <summary>Rest a day where you are: the men hunt and forage if the land allows.</summary>
    public void Camp()
    {
        var w = World;
        if (w.Afloat) { Say("You can only camp ashore."); return; }
        var t = w.Map[w.X, w.Y];
        int food = t switch { Terrain.Grass or Terrain.Forest or Terrain.River => (int)(w.Ashore * 0.9), Terrain.Jungle => (int)(w.Ashore * 0.6), _ => 0 };
        Pass(1);
        if (food > 0 && Mode == Mode.Voyage)
        {
            w.Food += food;
            Say($"A day's hunting and foraging: +{food} food.", 0xFF80E080);
            Play(Sfx.Forage);
        }
        else Say("The men rest, but this land gives nothing to eat.");
    }

    /// <summary>Look at where you stand (and meet the settlement there again).</summary>
    public void Look()
    {
        var w = World;
        if (!w.Afloat && w.SettlementAt(w.X, w.Y) is { } s) { Meet(s); return; }
        var (x, y) = Here;
        var tribe = w.Tribes[Peoples.Owner(w.Tribes, x, y)];
        Say(w.Afloat
            ? $"{Map.Describe(w.Map[x, y]).ToUpperInvariant()}. Home waters lie to the east."
            : $"{Map.Describe(w.Map[x, y]).ToUpperInvariant()} in {tribe.Country}, land of the {tribe.Name}.");
    }

    /// <summary>The voyage is over: everyone is dead or lost.</summary>
    private void Lost()
    {
        var w = World;
        CloseAll();
        w.Men = 0;
        w.Gold = 0;
        Tell("The expedition is lost", "No one comes home from this voyage. In time the Crown hears the news, and outfits another captain - it is you, given one more chance, but the gold you carried is gone.", () => ComeHome(wrecked: true));
    }
}
