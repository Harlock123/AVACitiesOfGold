
namespace AVACitiesOfGold.Game;

/// <summary>How a panel is drawn beyond its title, text and options.</summary>
public enum PanelKind { Plain, Encounter, Court, Ending }

/// <summary>A choice on a panel. <see cref="Adjust"/> handles Left and Right on the option, if anything.</summary>
public sealed record Opt(string Label, Action? Act, Action<int>? Adjust = null, bool Enabled = true, string? Note = null);

/// <summary>
/// A menu or message over the game: a title, some paragraphs, and options to pick with the arrows and
/// Enter (or their number). Panels stack; Escape runs <see cref="Back"/>, if the panel has one.
/// </summary>
public sealed class Panel
{
    public string Title { get; set; } = "";
    public List<string> Text { get; } = [];
    public List<Opt> Options { get; } = [];
    public int Selected { get; set; }
    public Action? Back { get; set; }
    public PanelKind Kind { get; set; }
    /// <summary>The settlement the panel is about, pictured beside it.</summary>
    public World.Settlement? Place { get; set; }
    /// <summary>When set, typed letters go into this buffer; Enter hands it to <see cref="OnEntry"/>.</summary>
    public string? Entry { get; set; }
    public Action<string>? OnEntry { get; set; }
    /// <summary>Builds the panel afresh, so a menu can show up-to-date numbers after a choice.</summary>
    public Func<Panel>? Make { get; set; }
    /// <summary>Left and Right on the panel as a whole (when the option has no adjuster): next character, say.</summary>
    public Action<int>? Turn { get; set; }

    public Panel Say(string text)
    {
        Text.Add(text);
        return this;
    }

    public Panel Add(string label, Action? act, bool enabled = true, string? note = null)
    {
        Options.Add(new Opt(label, act, null, enabled, note));
        return this;
    }

    public Panel AddAdjust(string label, Action<int> adjust, Action? act = null, string? note = null)
    {
        Options.Add(new Opt(label, act, adjust, true, note));
        return this;
    }
}
