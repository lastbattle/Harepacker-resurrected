namespace HaCreator.MapSimulator.UI;

/// <summary>
/// Selects the status-bar and window family used by the map simulator.
/// </summary>
public enum MapSimulatorUiFamily
{
    LegacyPreBigBang,
    BigBang,
    VUpdate
}

/// <summary>
/// Resolves the simulator UI family from the client-owned status-bar images.
/// </summary>
internal static class MapSimulatorUiFamilyResolver
{
    /// <summary>
    /// Resolves by asset ownership. Newer clients commonly retain older status-bar
    /// images for compatibility, so the newest existing owner wins.
    /// </summary>
    internal static MapSimulatorUiFamily ResolveFromStatusBarImages(
        bool hasStatusBar,
        bool hasStatusBar2,
        bool hasStatusBar3)
    {
        if (hasStatusBar3)
        {
            return MapSimulatorUiFamily.VUpdate;
        }

        if (hasStatusBar2)
        {
            return MapSimulatorUiFamily.BigBang;
        }

        return MapSimulatorUiFamily.LegacyPreBigBang;
    }
}
