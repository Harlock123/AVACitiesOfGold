using AVACitiesOfGold.World;

namespace AVACitiesOfGold.Tests;

internal static class Helpers
{
    public static SaveStore TempSaves() => new(Path.Combine(Path.GetTempPath(), "avacog-tests-" + Guid.NewGuid().ToString("N")));
}
