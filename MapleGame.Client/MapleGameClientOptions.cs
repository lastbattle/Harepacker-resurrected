using System;
using System.Linq;
using HaCreator.MapSimulator.Assets;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;

namespace MapleGame.Client;

/// <summary>
/// Composition options for the standalone client.  Keeping source selection in
/// a public value object lets a future launcher or host configure the client
/// without reproducing the command-line parser.
/// </summary>
public sealed class MapleGameClientOptions
{
    public string ImgDirectory { get; init; }
    public string WzDirectory { get; init; }
    public string HybridImgDirectory { get; init; }
    public int? MapId { get; init; }
    public string Portal { get; init; }
    public string ProfileDirectory { get; init; }
    public RenderResolution Resolution { get; init; } = RenderResolution.Res_1024x768;
    public WzMapleVersion WzVersion { get; init; } = WzMapleVersion.BMS;
    public byte[] CustomIv { get; init; }

    public void Validate()
    {
        ValidateAssetSource();
        if (!MapId.HasValue)
            throw new ArgumentException("A map ID is required.", nameof(MapId));
        if (MapId.Value < 0 || MapId.Value > 999999999)
            throw new ArgumentOutOfRangeException(nameof(MapId), MapId, "Map ID must be between 0 and 999999999.");
        if (!RenderResolutionCatalog.IsSelectable(Resolution))
            throw new ArgumentException("Resolution must be a selectable screen resolution.", nameof(Resolution));
    }

    public RuntimeAssetSource OpenAssetSource()
    {
        ValidateAssetSource();
        byte[] customIv = CustomIv?.ToArray();
        return !string.IsNullOrWhiteSpace(HybridImgDirectory)
            ? RuntimeAssetSourceFactory.OpenHybridDirectory(
                HybridImgDirectory,
                WzDirectory,
                WzVersion,
                customIv: customIv)
            : !string.IsNullOrWhiteSpace(WzDirectory)
                ? RuntimeAssetSourceFactory.OpenWzDirectory(
                    WzDirectory,
                    WzVersion,
                    customIv: customIv)
                : RuntimeAssetSourceFactory.OpenImgDirectory(ImgDirectory);
    }

    private void ValidateAssetSource()
    {
        if (!Enum.IsDefined(typeof(WzMapleVersion), WzVersion))
            throw new ArgumentException("WzVersion must be a defined MapleLib WZ version.", nameof(WzVersion));
        int selectedSources = (!string.IsNullOrWhiteSpace(ImgDirectory) ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(WzDirectory) ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(HybridImgDirectory) ? 1 : 0);
        if (selectedSources == 0)
            throw new ArgumentException("Specify an IMG, WZ, or hybrid asset source.");
        if (!string.IsNullOrWhiteSpace(HybridImgDirectory)
            && string.IsNullOrWhiteSpace(WzDirectory))
        {
            throw new ArgumentException("A hybrid source requires a WZ directory.", nameof(WzDirectory));
        }
        if (!string.IsNullOrWhiteSpace(ImgDirectory)
            && (!string.IsNullOrWhiteSpace(WzDirectory)
                || !string.IsNullOrWhiteSpace(HybridImgDirectory)))
        {
            throw new ArgumentException("Use IMG alone, or use hybrid IMG with WZ.", nameof(ImgDirectory));
        }
        if (CustomIv != null && CustomIv.Length != 4)
            throw new ArgumentException("A custom WZ IV must contain exactly four bytes.", nameof(CustomIv));
        if (CustomIv != null && WzVersion != WzMapleVersion.CUSTOM)
            throw new ArgumentException("A custom WZ IV requires WzVersion.CUSTOM.", nameof(WzVersion));
        if (WzVersion == WzMapleVersion.CUSTOM
            && !string.IsNullOrWhiteSpace(WzDirectory)
            && CustomIv == null)
        {
            throw new ArgumentException("WzVersion.CUSTOM requires a four-byte custom IV.", nameof(CustomIv));
        }
    }
}
