using System.Text.Json.Serialization;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;

namespace HaSharedLibrary.Configuration;

public sealed class HaRepackerUserSettings
{
    public enum UserSettingsThemeColor
    {
        Dark = 0,
        Light = 1
    }

    [JsonPropertyName("Indentation")]
    public int Indentation;

    [JsonPropertyName("LineBreakType")]
    [JsonConverter(typeof(JsonStringEnumConverter<LineBreak>))]
    public LineBreak LineBreakType = LineBreak.None;

    [JsonPropertyName("DefaultXmlFolder")]
    public string DefaultXmlFolder = string.Empty;

    [JsonPropertyName("UseApngIncompatibilityFrame")]
    public bool UseApngIncompatibilityFrame = true;

    [JsonPropertyName("AutoAssociate")]
    public bool AutoAssociate = true;

    [JsonPropertyName("Sort")]
    public bool Sort;

    [JsonPropertyName("SuppressWarnings")]
    public bool SuppressWarnings;

    [JsonPropertyName("ParseImagesInSearch")]
    public bool ParseImagesInSearch;

    [JsonPropertyName("SearchStringValues")]
    public bool SearchStringValues = true;

    [JsonPropertyName("DevImgSequences")]
    public bool DevImgSequences;

    [JsonPropertyName("CartesianPlane")]
    public bool CartesianPlane = true;

    [JsonPropertyName("DelayNextLoop")]
    public int DelayNextLoop = 60;

    [JsonPropertyName("PlanePosition")]
    public string PlanePosition = "Center";

    [JsonPropertyName("ThemeColor")]
    public int ThemeColor = (int)UserSettingsThemeColor.Light;

    [JsonPropertyName("EnableCrossHairDebugInformation")]
    public bool EnableCrossHairDebugInformation = true;

    [JsonPropertyName("EnableBorderDebugInformation")]
    public bool EnableBorderDebugInformation = true;

    [JsonPropertyName("ImageZoomLevel")]
    public double ImageZoomLevel = 3.0;

    [JsonPropertyName("AutoloadRelatedWzFiles")]
    public bool AutoloadRelatedWzFiles;
}

public sealed class HaRepackerApplicationSettings
{
    [JsonPropertyName("WindowMaximized")]
    public bool WindowMaximized;

    [JsonPropertyName("WindowWidth")]
    public int Width = 1024;

    [JsonPropertyName("WindowHeight")]
    public int Height = 768;

    [JsonPropertyName("FirstRun")]
    public bool FirstRun = true;

    [JsonPropertyName("LastBrowserPath")]
    public string LastBrowserPath = string.Empty;

    [JsonPropertyName("MapleStoryVersion")]
    [JsonConverter(typeof(JsonStringEnumConverter<WzMapleVersion>))]
    public WzMapleVersion MapleVersion = WzMapleVersion.BMS;

    [JsonPropertyName("MapleStoryVersion_CustomEncryptionName")]
    public string MapleVersion_CustomEncryptionName = "Default";

    [JsonPropertyName("MapleStoryVersion_CustomAESUserKey")]
    public string MapleVersion_CustomAESUserKey = string.Empty;

    [JsonPropertyName("MapleStoryVersion_EncryptionBytes")]
    public string MapleVersion_CustomEncryptionBytes = "0x00-0x00-0x00-0x00";
}
