using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Game;

public sealed partial class GameEngine
{
    public void ShowTitle()
    {
        Mode = Mode.Title;
        Demo = null;
        Idle = 0;
        ShowMap = false;
        CloseAll();
        Play(Sfx.MusicStop);
        Play(Sfx.TitleTune);
        var p = new Panel { Title = "" };
        p.Add("Sail for a new world", () => NewGame(Rng.Next(1_000_000)), note: "A new land, never seen before, made for this voyage.");
        p.Add("Continue a saved career", () => LoadMenu(), Saves.Any());
        p.Add("How to play", Help);
        p.Add("Quit", () => QuitRequested?.Invoke());
        Push(p);
    }

    /// <summary>A new career in a new world: the Crown outfits your first voyage.</summary>
    public void NewGame(int seed)
    {
        World = WorldState.New(seed);
        Log.Clear();
        CloseAll();
        ShowMap = false;
        Mode = Mode.Court;
        Tell("The Crown's commission",
            "Far to the west, the sailors say, lies a land no ship from home has ever reached - and in it, say the old stories, the Seven Cities of Gold. " +
            "The Crown gives you ships, men and goods to trade. Find the land. Meet its peoples. Bring back gold, and news. How you do it is up to you.",
            SetSail);
    }

    /// <summary>The Crown outfits the next voyage, by your rank, and the ships sail west.</summary>
    public void SetSail()
    {
        var w = World;
        w.Voyage++;
        int rank = w.Rank;
        w.Ships = Math.Min(5, 2 + rank);
        w.Men = Math.Min(110, 30 + 15 * rank);
        w.Goods = 60 + 40 * rank;
        w.Food = w.Ration * 90;
        w.Gold = 0;
        w.Afloat = true;
        w.AtHome = false;
        w.ShipX = Map.HomeWaters - 1;
        w.ShipY = Map.Height / 2;
        w.See(w.ShipX, w.ShipY, 4);
        CloseAll();
        Mode = Mode.Voyage;
        Play(Sfx.MusicStop);
        Play(Sfx.Sea);
        Say($"Voyage {w.Voyage}: {w.Ships} ships, {w.Men} men, {w.Goods} goods, {w.DaysOfFood} days of food. West, into the unknown!", 0xFFFFD860);
    }

    /// <summary>Back in home waters: gold to the Crown, a report, perhaps a promotion.</summary>
    private void ComeHome(bool wrecked = false)
    {
        var w = World;
        CloseAll();
        ShowMap = false;
        Mode = Mode.Court;
        w.AtHome = true;
        int rankBefore = w.Rank;
        int revenue = w.Posts * 40;
        int brought = w.Gold;
        w.GoldDelivered += brought + revenue;
        w.Gold = 0;
        w.Day += 60;
        Play(Sfx.MusicStop);
        Play(Sfx.Fanfare);
        var p = new Panel { Title = "The royal court", Kind = PanelKind.Court };
        if (wrecked) p.Say("The court is quiet. The Crown has heard of your lost voyage.");
        else p.Say($"You kneel before the throne and lay down {brought} gold.{(revenue > 0 ? $" Your trading posts have sent {revenue} more." : "")}");
        p.Say($"So far: {w.GoldDelivered} gold delivered, {w.Found} settlements found, {w.CitiesFound} of the seven golden cities reached, {w.Posts} trading posts, {w.Explored}% of the land charted.");
        if (w.Rank > rankBefore) { p.Say($"The Crown raises you to {WorldState.Ranks[w.Rank]}!"); Play(Sfx.Coin); }
        if (w.Year > CareerYears)
        {
            p.Say("Your sailing days are over: the Crown grants you an honourable retirement.");
            p.Add("Retire", Retire);
        }
        else
        {
            p.Add($"Set sail again (year {w.Year})", SetSail);
            p.Add("Save your career", SaveMenu);
            p.Add("Retire", Retire);
        }
        Push(p);
    }

    private void Retire()
    {
        var w = World;
        w.Retired = true;
        Mode = Mode.Ended;
        CloseAll();
        Play(Sfx.MusicStop);
        Play(Sfx.Ending);
        string legacy = w.Destroyed > w.Posts + 2
            ? "History will remember you for the cities you burned, and the gold you took from them."
            : w.Posts >= 3
                ? "History will remember you as the one who came in friendship, and left trade and friends behind."
                : "History will remember you as an explorer who went where no one from home had been.";
        var p = new Panel { Title = $"{WorldState.Ranks[w.Rank]} - retired", Kind = PanelKind.Ending };
        p.Say($"After {w.Voyage} {(w.Voyage == 1 ? "voyage" : "voyages")} and {w.Year} {(w.Year == 1 ? "year" : "years")}, you hang up your sword and your charts.");
        p.Say($"Gold delivered {w.GoldDelivered}. Settlements found {w.Found}. Golden cities reached {w.CitiesFound} of 7. Trading posts {w.Posts}. Land charted {w.Explored}%. Settlements destroyed {w.Destroyed}. Men lost {w.MenLost}.");
        p.Say($"RENOWN: {w.Score}. {legacy}");
        p.Add("Return to the title", ShowTitle);
        Push(p);
    }

    // ───────────────────────────── menus ─────────────────────────────

    private void VoyageMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "The captain's cabin", Back = Pop };
            p.Add("Carry on", Pop);
            p.Add("Look at the chart", () => { Pop(); ShowMap = true; });
            p.Add("Save the voyage", SaveMenu);
            p.Add("Load a saved career", () => LoadMenu());
            p.Add("How to play", Help);
            p.Add("Quit to the title", () =>
            {
                var q = new Panel { Title = "Quit to the title?", Back = Pop };
                q.Say("Anything not saved will be lost.");
                q.Add("Yes, quit", ShowTitle);
                q.Add("No, carry on", Pop);
                q.Selected = 1;
                Push(q);
            });
            return p;
        });
    }

    public void SaveMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Save", Back = Pop };
            for (int slot = 1; slot <= SaveStore.Slots; slot++)
            {
                int s = slot;
                p.Add(Saves.Describe(s), () => SaveTo(s));
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public void SaveTo(int slot)
    {
        World.Saved = DateTime.Now;
        try
        {
            Saves.Save(slot, World);
            Pop();
            Say($"Saved in slot {slot}.", 0xFF80FF80);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Tell("Not saved", "The game could not be saved: " + e.Message);
        }
    }

    public void LoadMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Load", Back = Pop };
            for (int slot = 1; slot <= SaveStore.Slots; slot++)
            {
                int s = slot;
                string text = Saves.Describe(s);
                p.Add(text, () => LoadFrom(s), !text.EndsWith("empty"));
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public bool LoadFrom(int slot)
    {
        var w = Saves.Load(slot);
        if (w is null) return false;
        World = w;
        Log.Clear();
        CloseAll();
        Demo = null;
        ShowMap = false;
        Play(Sfx.MusicStop);
        if (w.AtHome)
        {
            Mode = Mode.Court;
            var p = new Panel { Title = "The royal court", Kind = PanelKind.Court };
            p.Say($"{WorldState.Ranks[w.Rank]}, after {w.Voyage} voyages. Renown {w.Score}.");
            p.Add($"Set sail again (year {w.Year})", SetSail);
            p.Add("Retire", Retire);
            Push(p);
        }
        else
        {
            Mode = Mode.Voyage;
            Play(w.Afloat ? Sfx.Sea : Sfx.Land);
            Say($"Loaded slot {slot}.", 0xFFFFD860);
        }
        return true;
    }

    public void Help()
    {
        var p = new Panel { Title = "How to play", Back = Pop };
        p.Say("SAILING: the arrow keys sail your ships. The land lies west; home waters are at the far east edge of the chart - sail into them to go home. Sailing into land puts your men ashore; walking back to the ships takes them aboard.");
        p.Say("ASHORE: the arrows walk. Forests, jungle, hills and mountains are slow, and everyone eats every day. F camps for a day to hunt and forage. Walk into a settlement to meet its people: trade goods for gold or food, give gifts, feast, ask for guides, found a trading post - or take what you want by force, and live with it.");
        p.Say("THE CHART: M shows everything you have seen. Space looks around. Esc opens the cabin: save, load, help, quit.");
        p.Say("AT HOME: deliver your gold to the Crown. Renown brings promotion, and each rank brings more ships, men and goods for the next voyage. After twenty years you retire, or retire when you choose.");
        p.Add("Back", Pop);
        Push(p);
    }

    // ───────────────────────────── the attract demo ─────────────────────────────

    public void StartDemo()
    {
        World = WorldState.New(Rng.Next(1_000_000));
        Log.Clear();
        CloseAll();
        Mode = Mode.Court;
        SetSail();
        Demo = new DemoPilot(this);
    }

    public void EndDemo()
    {
        Demo = null;
        World = new WorldState();
        ShowTitle();
    }
}
