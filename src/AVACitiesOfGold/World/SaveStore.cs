namespace AVACitiesOfGold.World;

/// <summary>
/// Saved games on disk, one JSON file per slot, in the user's application-data folder (or a folder
/// of the caller's choosing, which is how the tests keep out of the real one).
/// </summary>
public sealed class SaveStore
{
    public const int Slots = 5;

    public string Folder { get; }

    public SaveStore(string? folder = null) =>
        Folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AVACitiesOfGold");

    private string PathFor(int slot) => Path.Combine(Folder, $"save{slot}.json");

    public void Save(int slot, WorldState world)
    {
        world.Saved = DateTime.Now;
        Directory.CreateDirectory(Folder);
        string tmp = PathFor(slot) + ".tmp";
        File.WriteAllText(tmp, world.ToJson());
        File.Move(tmp, PathFor(slot), overwrite: true);
    }

    public WorldState? Load(int slot)
    {
        try
        {
            return File.Exists(PathFor(slot)) ? WorldState.FromJson(File.ReadAllText(PathFor(slot))) : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A line describing a slot for the load and save menus.</summary>
    public string Describe(int slot)
    {
        var w = Load(slot);
        if (w is null) return $"Slot {slot}: empty";
        return $"Slot {slot}: {WorldState.Ranks[w.Rank].ToLowerInvariant()}, voyage {w.Voyage}, year {w.Year}, renown {w.Score}";
    }

    public bool Any() => Enumerable.Range(1, Slots).Any(s => File.Exists(PathFor(s)));
}
