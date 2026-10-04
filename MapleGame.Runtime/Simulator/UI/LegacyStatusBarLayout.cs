using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI;

/// <summary>
/// Fixed coordinates authored by the legacy StatusBar.img client skin.
/// </summary>
internal static class LegacyStatusBarLayout
{
    internal const int FrameWidth = 800;
    internal const int FrameHeight = 71;
    internal static readonly Point GaugeOrigin = new(198, 40);
    internal static readonly Point PrimaryButtonOrigin = new(540, 37);
    internal static readonly Point ShortcutOrigin = new(523, 17);
    internal static readonly Point LevelTextOffset = new(44, -1);
    internal static readonly Point JobTextOffset = new(74, -5);
    internal static readonly Point NameTextOffset = new(74, 5);
    internal const float LevelTextScale = 1.35f;
    internal const int PrimaryButtonSpacing = 54;
    internal const int ShortcutSpacing = 28;

    internal static Point GetPrimaryButtonPosition(int index) =>
        new(PrimaryButtonOrigin.X + (index * PrimaryButtonSpacing), PrimaryButtonOrigin.Y);

    internal static Point GetShortcutPosition(int index) =>
        new(ShortcutOrigin.X + (index * ShortcutSpacing), ShortcutOrigin.Y);
}
