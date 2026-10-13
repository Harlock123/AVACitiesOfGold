using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Game;

public enum Mode { Title, Voyage, Court, Ended }

/// <summary>
/// The whole game, apart from drawing it: a career of voyages from home across the ocean to the new
/// land and back. Keys arrive by name ("Up", "Enter", "M"), so the tests and the attract demo play it
/// just as a person does.
/// </summary>
public sealed partial class GameEngine
{
    public const double DemoAfter = 30;
    public const double DemoLength = 75;
    public const int CareerYears = 20;

    public Mode Mode { get; private set; } = Mode.Title;
    public WorldState World { get; private set; } = new();
    public Rng Rng { get; }
    public SaveStore Saves { get; }
    public List<Panel> Panels { get; } = [];
    public Panel? Top => Panels.Count > 0 ? Panels[^1] : null;
    public List<(string Text, uint Colour)> Log { get; } = [];
    public bool ShowMap { get; set; }
    public double Clock { get; private set; }
    public double Idle { get; private set; }
    public DemoPilot? Demo { get; private set; }
    public bool InDemo => Demo is not null;
    public double Bumped { get; private set; }
    public event Action<Sfx>? Sound;
    public event Action? QuitRequested;

    public GameEngine(int seed, SaveStore? saves = null)
    {
        Rng = new Rng((ulong)seed * 2654435761UL + 1);
        Saves = saves ?? new SaveStore();
        ShowTitle();
    }

    public void Play(Sfx s) => Sound?.Invoke(s);

    public void Say(string text, uint colour = 0xFFE8E0D0)
    {
        Log.Add((text, colour));
        if (Log.Count > 40) Log.RemoveAt(0);
    }

    public void Update(double dt)
    {
        Clock += dt;
        if (Bumped > 0) Bumped -= dt;
        if (Demo is not null)
        {
            Demo.Update(dt);
            if (Demo.Elapsed > DemoLength || Mode != Mode.Voyage) EndDemo();
        }
        else if (Mode == Mode.Title && Panels.Count == 1)
        {
            Idle += dt;
            if (Idle > DemoAfter) StartDemo();
        }
    }

    // ───────────────────────────── panels ─────────────────────────────

    public Panel Push(Panel p)
    {
        Panels.Add(p);
        return p;
    }

    public Panel Show(Func<Panel> make)
    {
        var p = make();
        p.Make = make;
        return Push(p);
    }

    public void Pop()
    {
        if (Panels.Count > 0) Panels.RemoveAt(Panels.Count - 1);
        Refresh();
    }

    public void CloseAll() => Panels.Clear();

    public void Refresh()
    {
        if (Top is not { Make: { } make } old) return;
        var p = make();
        p.Make = make;
        p.Selected = Math.Clamp(old.Selected, 0, Math.Max(0, p.Options.Count - 1));
        p.Place = old.Place;
        Panels[^1] = p;
    }

    public void Tell(string title, string text, Action? then = null)
    {
        var p = new Panel { Title = title }.Say(text);
        p.Add("Continue", () => { Pop(); then?.Invoke(); });
        p.Back = () => { Pop(); then?.Invoke(); };
        Push(p);
    }

    // ───────────────────────────── keys ─────────────────────────────

    public void Press(string key)
    {
        if (Demo is not null)
        {
            EndDemo();
            return;
        }
        Input(key);
    }

    internal void Input(string key)
    {
        Idle = 0;
        if (Top is { } p)
        {
            PanelKey(p, key);
            return;
        }
        if (Mode != Mode.Voyage) return;
        switch (key)
        {
            case "Up": Move(Dir.North); break;
            case "Down": Move(Dir.South); break;
            case "Left": Move(Dir.West); break;
            case "Right": Move(Dir.East); break;
            case "M" or "Tab": ShowMap = !ShowMap; Play(Sfx.Menu); break;
            case "F": Camp(); break;
            case "Space" or "Enter": Look(); break;
            case "H": Help(); break;
            case "Esc": VoyageMenu(); break;
        }
    }

    public void Type(string text) { }

    private void PanelKey(Panel p, string key)
    {
        var opts = p.Options;
        switch (key)
        {
            case "Up":
                if (opts.Count > 0) { p.Selected = (p.Selected + opts.Count - 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Down":
                if (opts.Count > 0) { p.Selected = (p.Selected + 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Enter" or "Space":
                Choose(p, p.Selected);
                return;
            case "Esc" or "Back":
                p.Back?.Invoke();
                return;
        }
        if (key.Length == 1 && char.IsDigit(key[0]) && key[0] != '0')
        {
            int n = key[0] - '1';
            if (n < opts.Count) { p.Selected = n; Choose(p, n); }
        }
    }

    private void Choose(Panel p, int index)
    {
        if (index < 0 || index >= p.Options.Count) return;
        var o = p.Options[index];
        if (!o.Enabled || o.Act is null) return;
        Play(Sfx.Pick);
        o.Act();
        if (Top == p) Refresh();
    }
}
