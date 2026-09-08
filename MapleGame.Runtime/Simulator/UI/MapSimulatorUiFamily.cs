using MapleLib.Img;

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

    /// <summary>
    /// Resolves a family using extracted version metadata and the presence of the
    /// post-Big-Bang marker in UIWindow2.img.
    /// </summary>
    internal static MapSimulatorUiFamily Resolve(VersionInfo versionInfo, bool hasBigBangMarker)
    {
        return Resolve(
            isVUpdate: versionInfo?.IsVUpdate ?? false,
            isPreBigBang: versionInfo?.IsPreBB ?? false,
            hasBigBangMarker);
    }

    /// <summary>
    /// Pure overload for callers that already have the relevant flags. Keeping the
    /// marker probe outside this method makes family selection deterministic and
    /// straightforward to exercise without loading WZ assets.
    /// </summary>
    internal static MapSimulatorUiFamily Resolve(
        bool isVUpdate,
        bool isPreBigBang,
        bool hasBigBangMarker)
    {
        // A V update client uses the modern UI family regardless of legacy metadata
        // or marker state.
        if (isVUpdate)
        {
            return MapSimulatorUiFamily.VUpdate;
        }

        // Missing marker data is intentionally treated as legacy. This preserves
        // the legacy/beta behavior when UIWindow2.img is unavailable or incomplete.
        if (isPreBigBang || !hasBigBangMarker)
        {
            return MapSimulatorUiFamily.LegacyPreBigBang;
        }

        return MapSimulatorUiFamily.BigBang;
    }
}
