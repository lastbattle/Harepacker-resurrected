using System;
using System.IO;
using MapleLib.Img;
using MapleLib.WzLib;

namespace HaCreator.MapSimulator.Assets;

/// <summary>Opens a source owned exclusively by one game session.</summary>
public static class RuntimeAssetSourceFactory
{
    public static RuntimeAssetSource OpenImgDirectory(string versionDirectory,
        RuntimeAssetOverrideStore overrides = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);
        string path = Path.GetFullPath(versionDirectory);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);

        var configuration = new HaCreatorConfig();
        configuration.HotSwap.Enabled = false;
        var source = new ImgFileSystemDataSource(path, configuration);
        try
        {
            return new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Owned, overrides);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a legacy MapleStory WZ installation for one runtime session. The
    /// manager is deliberately not installed into MapleLib's process-wide
    /// compatibility slot, so an editor-owned WzFileManager remains active.
    /// </summary>
    public static RuntimeAssetSource OpenWzDirectory(
        string wzDirectory,
        WzMapleVersion mapleVersion = WzMapleVersion.BMS,
        RuntimeAssetOverrideStore overrides = null,
        byte[] customIv = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wzDirectory);
        string path = Path.GetFullPath(wzDirectory);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        ValidateEncryptionOptions(mapleVersion, customIv);

        var configuration = CreateLegacyConfiguration(path);
        var source = new WzFileDataSource(
            path,
            configuration,
            registerAsGlobal: false,
            mapleVersion: mapleVersion,
            customIv: customIv);
        try
        {
            source.Initialize(mapleVersion);
            return new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Owned, overrides);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens IMG data with an optional legacy WZ fallback. Both sources are
    /// session-owned and the fallback manager does not replace the editor's
    /// global WzFileManager.
    /// </summary>
    public static RuntimeAssetSource OpenHybridDirectory(
        string imgVersionDirectory,
        string wzDirectory = null,
        WzMapleVersion mapleVersion = WzMapleVersion.BMS,
        RuntimeAssetOverrideStore overrides = null,
        byte[] customIv = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imgVersionDirectory);
        string imgPath = Path.GetFullPath(imgVersionDirectory);
        if (!Directory.Exists(imgPath)) throw new DirectoryNotFoundException(imgPath);
        if (string.IsNullOrWhiteSpace(wzDirectory))
            return OpenImgDirectory(imgPath, overrides);

        string wzPath = Path.GetFullPath(wzDirectory);
        if (!Directory.Exists(wzPath)) throw new DirectoryNotFoundException(wzPath);
        ValidateEncryptionOptions(mapleVersion, customIv);

        var configuration = CreateLegacyConfiguration(wzPath);
        var source = new HybridDataSource(
            imgPath,
            configuration,
            registerWzManagerAsGlobal: false,
            mapleVersion: mapleVersion,
            customIv: customIv);
        try
        {
            if (!source.IsInitialized)
                throw new InvalidDataException($"No IMG or WZ data source could be opened for '{imgPath}'.");
            return new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Owned, overrides);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static HaCreatorConfig CreateLegacyConfiguration(string wzDirectory)
    {
        var configuration = new HaCreatorConfig();
        configuration.DataSourceMode = DataSourceMode.WzFiles;
        configuration.Legacy.WzFilePath = wzDirectory;
        configuration.HotSwap.Enabled = false;
        return configuration;
    }

    private static void ValidateEncryptionOptions(WzMapleVersion mapleVersion, byte[] customIv)
    {
        if (customIv != null && customIv.Length != 4)
            throw new ArgumentException("A WZ IV must contain exactly four bytes.", nameof(customIv));
        if (mapleVersion == WzMapleVersion.CUSTOM && customIv == null)
            throw new ArgumentException(
                "WzMapleVersion.CUSTOM requires an explicit four-byte IV.",
                nameof(customIv));
        if (customIv != null && mapleVersion != WzMapleVersion.CUSTOM)
            throw new ArgumentException(
                "An explicit WZ IV requires WzMapleVersion.CUSTOM.",
                nameof(mapleVersion));
    }
}
