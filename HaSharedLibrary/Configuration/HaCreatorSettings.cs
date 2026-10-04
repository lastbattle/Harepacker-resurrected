using System.Drawing;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace HaSharedLibrary.Configuration;

public static class HaCreatorUserSettings
{
    public static bool ShowErrorsMessage = true;
    public static RenderResolution SimulateResolution = RenderResolution.Res_1024x768;
    public static bool ClipText;
    public static Color TabColor = Color.LightSteelBlue;
    public static int LineWidth = 1;
    public static int DotWidth = 3;
    public static XnaColor SelectSquare = new(0, 0, 255, 255);
    public static XnaColor SelectSquareFill = new(0, 0, 200, 200);
    public static XnaColor SelectedColor = new(0, 0, 255, 250);
    public static XnaColor VRColor = new(0, 0, 255, 255);
    public static XnaColor FootholdColor = XnaColor.Red;
    public static XnaColor RopeColor = XnaColor.Green;
    public static XnaColor ChairColor = XnaColor.Orange;
    public static XnaColor ToolTipColor = XnaColor.SkyBlue;
    public static XnaColor ToolTipFill = new(0, 0, 255, 100);
    public static XnaColor ToolTipSelectedFill = new(0, 0, 255, 150);
    public static XnaColor ToolTipCharFill = new(0, 255, 0, 100);
    public static XnaColor ToolTipCharSelectedFill = new(0, 255, 0, 150);
    public static XnaColor ToolTipBindingLine = XnaColor.Magenta;
    public static XnaColor MiscColor = XnaColor.Brown;
    public static XnaColor MiscFill = new(150, 75, 0, 100);
    public static XnaColor MiscSelectedFill = new(150, 75, 0, 150);
    public static XnaColor OriginColor = XnaColor.LightGreen;
    public static XnaColor MinimapBoundColor = XnaColor.DarkOrange;
    public static int NonActiveAlpha = 63;
    public static int Mobrx0Offset = 200;
    public static int Mobrx1Offset = 200;
    public static int Npcrx0Offset = 20;
    public static int Npcrx1Offset = 20;
    public static int defaultMobTime;
    public static int defaultReactorTime;
    public static int AntiMacroScreenshotSaveLocation;
    public static float SnapDistance = 10;
    public static float SignificantDistance = 10;
    public static int ScrollDistance = 90;
    public static double ScrollFactor = 1;
    public static double ScrollBase = 1.05;
    public static double ScrollExponentFactor = 1;
    public static int zShift = 1;
    public static int HiddenLifeR = 127;
    public static string FontName = "Arial";
    public static int FontSize = 13;
    public static FontStyle FontStyle = FontStyle.Regular;
    public static int dotDescriptionBoxSize = 100;
    public static int ImageViewerHeight = 100;
    public static int ImageViewerWidth = 100;
    public static bool useMiniMap = true;
    public static bool useSnapping = true;
    public static bool emulateParallax = true;
    public static bool suppressWarnings;
    public static bool FixFootholdMispositions = true;
    public static bool InverseUpDown = true;
    public static bool BackupEnabled = true;
    public static int BackupIdleTime = 5000;
    public static int BackupMaxTime = 60000;
}

public static class HaCreatorApplicationSettings
{
    public static int MapleVersionIndex = 3;
    public static int MapleStoryClientLocalisation = 1;
    public static string MapleFoldersList = string.Empty;
    public static int MapleFolderIndex;
    public static string MapleStoryDataBasePath = string.Empty;
    public static ItemTypes theoreticalVisibleTypes = ItemTypes.All;
    public static ItemTypes theoreticalEditedTypes = ItemTypes.All ^ ItemTypes.Backgrounds;
    public static Size LastMapSize = new(800, 800);
    public static int lastRadioIndex = 3;
    public static bool randomTiles = true;
    public static bool InfoMode;
    public static bool AnimateMapObjectPreviews = true;
    public static bool HideLifeEntriesWithoutImages = true;
    public static int lastDefaultLayer;
    public static bool lastAllLayers = true;
    public static string LastMapImportSourcePath = string.Empty;
    public static bool ShowObjectViewerOnLoad = true;
    public static double ObjectViewerWidth = 350;
    public static double ObjectViewerHeight = 600;
}
