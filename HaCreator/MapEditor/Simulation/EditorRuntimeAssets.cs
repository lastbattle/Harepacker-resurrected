using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.Wz;
using MapleLib;
using MapleLib.Img;
using MapleLib.WzLib;
using System.Linq;
using System.Threading;

namespace HaCreator.MapEditor.Simulation;

/// <summary>Owns detached asset copies for the lifetime of an editor preview.</summary>
public sealed class EditorRuntimeAssets : IDisposable
{
    private readonly RuntimeAssetSource assets;
    private readonly IDataSourcePreviewLease previewLease;
    private readonly IDisposable writeLease;
    private readonly WzFileDataSource borrowedWzAdapter;
    private int disposed;

    public EditorRuntimeAssets(IDataSource source, WzFileManager wzManager, WzInformationManager information,
        IEnumerable<RuntimeMapDefinition> mapDefinitions)
    {
        if (source == null)
        {
            borrowedWzAdapter = new WzFileDataSource(
                wzManager ?? throw new ArgumentNullException(nameof(wzManager)), ownsManager: false);
            source = borrowedWzAdapter;
        }
        var overrides = new RuntimeAssetOverrideStore();
        try
        {
            // The detached session reads the same source generation as the
            // editor. Hold a host-side lease for its lifetime so save/repack
            // commands can fail fast instead of changing files underneath it.
            writeLease = EditorRuntimeWriteCoordinator.EnterPreview();
            foreach (RuntimeMapDefinition mapDefinition in mapDefinitions)
            {
                foreach (RuntimeAssetKey key in mapDefinition.AssetOverrides.Keys)
                {
                    if (mapDefinition.TryCreateAssetImageRoot(key, out var image))
                    {
                        using (image) overrides.Set(key, image);
                    }
                }
            }
            // Every preview source gets an independent owner. In particular, do
            // not adapt an editor WzFileManager directly: its legacy global slot
            // and lazy outlink resolution are shared with the editor process.
            assets = OpenSessionSource(source, wzManager, overrides, out previewLease);
            Services = new RuntimeDataServices(assets, new HaCreatorRuntimeAssetCatalog(information, assets));
        }
        catch
        {
            overrides.Dispose();
            Dispose();
            throw;
        }
    }

    public IRuntimeDataServices Services { get; }

    private static RuntimeAssetSource OpenSessionSource(
        IDataSource source,
        WzFileManager wzManager,
        RuntimeAssetOverrideStore overrides,
        out IDataSourcePreviewLease previewLease)
    {
        previewLease = null;
        if (source is ImgFileSystemDataSource imgSource &&
            !string.IsNullOrWhiteSpace(imgSource.Manager?.VersionPath))
        {
            return RuntimeAssetSourceFactory.OpenImgDirectory(imgSource.Manager.VersionPath, overrides);
        }

        if (source is HybridDataSource hybridSource)
        {
            string imgPath = hybridSource.ImgSource?.Manager.VersionPath;
            string wzPath = hybridSource.WzSource?.WzRootPath;
            WzMapleVersion version = hybridSource.WzSource?.MapleVersion ?? WzMapleVersion.BMS;
            byte[] customIv = hybridSource.WzSource?.CustomIv;
            if (!string.IsNullOrWhiteSpace(imgPath))
            {
                return RuntimeAssetSourceFactory.OpenHybridDirectory(
                    imgPath,
                    wzPath,
                    version,
                    overrides,
                    customIv);
            }

            if (!string.IsNullOrWhiteSpace(wzPath))
            {
                return RuntimeAssetSourceFactory.OpenWzDirectory(
                    wzPath,
                    version,
                    overrides,
                    customIv);
            }
        }

        if (source is WzFileDataSource wzSource &&
            !string.IsNullOrWhiteSpace(wzSource.WzRootPath))
        {
            return RuntimeAssetSourceFactory.OpenWzDirectory(
                wzSource.WzRootPath,
                wzSource.MapleVersion,
                overrides,
                wzSource.CustomIv);
        }

        if (wzManager != null && !string.IsNullOrWhiteSpace(wzManager.BaseDirectory))
        {
            WzMapleVersion version = wzManager.WzFileList.FirstOrDefault()?.MapleVersion ?? WzMapleVersion.BMS;
            return RuntimeAssetSourceFactory.OpenWzDirectory(wzManager.BaseDirectory, version, overrides);
        }

        if (source is IDataSourcePreviewLeaseProvider leaseProvider)
        {
            previewLease = leaseProvider.AcquirePreviewLease();
            if (previewLease == null)
                throw new InvalidOperationException(
                    $"The data source '{source.GetType().FullName}' returned no preview lease. " +
                    "Implement IDataSourcePreviewLeaseProvider with an independent snapshot or lease.");

            IDataSource leasedSource = previewLease.DataSource;
            if (leasedSource == null || ReferenceEquals(leasedSource, source))
            {
                previewLease.Dispose();
                previewLease = null;
                throw new InvalidOperationException(
                    $"The data source '{source.GetType().FullName}' did not provide an independent " +
                    "preview source. The lease must expose a detached source or a pinned cache generation.");
            }

            try
            {
                // The lease owns the source and pins its lifetime. RuntimeAssetSource
                // therefore borrows the leased facade; EditorRuntimeAssets disposes
                // the lease after the runtime facade has released its detached cache.
                return new RuntimeAssetSource(
                    leasedSource,
                    RuntimeAssetSourceOwnership.Borrowed,
                    overrides);
            }
            catch
            {
                previewLease.Dispose();
                previewLease = null;
                throw;
            }
        }

        throw new InvalidOperationException(
            $"Map preview cannot isolate data source '{source.GetType().FullName}'. " +
            "Provide an independent IMG/WZ source or implement " +
            "IDataSourcePreviewLeaseProvider with a detached snapshot/lease.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        try
        {
            assets?.Dispose();
        }
        finally
        {
            try
            {
                previewLease?.Dispose();
            }
            finally
            {
                try
                {
                    borrowedWzAdapter?.Dispose();
                }
                finally
                {
                    writeLease?.Dispose();
                }
            }
        }
    }
}
