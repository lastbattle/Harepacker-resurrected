using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.Img;
using MapleLib.WzLib;

namespace HaCreator.WorldMap;

public sealed record WorldMapSourceCapabilities(
    WorldMapSourceMode Mode,
    bool CanCreate,
    bool CanDelete,
    bool SupportsAtomicBatch,
    bool WritesImmediately,
    string DestinationDescription);

public sealed record WorldMapImageCandidate(string ImageName, WzImage Image, string RelativePath = null);

public sealed record WorldMapBatchSaveResult(
    bool Succeeded,
    IReadOnlyList<string> AffectedImages,
    IReadOnlyList<string> Errors,
    WorldMapSourceMode Mode);

/// <summary>
/// Source-aware operations for WorldMap images.  This keeps file-system/WZ ownership
/// decisions out of the WPF workspace and provides a single reviewable change-set path.
/// </summary>
public sealed class WorldMapSourceOperations
{
    private readonly IDataSource _source;

    public WorldMapSourceOperations(IDataSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public IDataSource DataSource => _source;

    public WorldMapSourceMode Mode => _source switch
    {
        ImgFileSystemDataSource => WorldMapSourceMode.Img,
        WzFileDataSource => WorldMapSourceMode.Wz,
        HybridDataSource => WorldMapSourceMode.Hybrid,
        _ => WorldMapSourceMode.Unknown
    };

    public WorldMapSourceCapabilities Capabilities => new(
        Mode,
        CanCreate: Mode is WorldMapSourceMode.Img or WorldMapSourceMode.Wz or WorldMapSourceMode.Hybrid,
        CanDelete: Mode is WorldMapSourceMode.Img or WorldMapSourceMode.Wz,
        SupportsAtomicBatch: Mode == WorldMapSourceMode.Img,
        WritesImmediately: Mode is WorldMapSourceMode.Img or WorldMapSourceMode.Hybrid,
        DestinationDescription: Mode switch
        {
            WorldMapSourceMode.Img => "IMG filesystem (Map/WorldMap)",
            WorldMapSourceMode.Wz => "owning WZ file (pending repack)",
            WorldMapSourceMode.Hybrid => "hybrid destination (IMG preferred)",
            _ => "active data source"
        });

    public IReadOnlyList<string> EnumerateImageNames()
    {
        IEnumerable<string> names = _source.GetImageNamesInDirectory("Map", "WorldMap") ?? Enumerable.Empty<string>();
        return names.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public WzImage Load(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;
        string normalized = imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName : imageName + ".img";
        if (Mode == WorldMapSourceMode.Wz || (Mode == WorldMapSourceMode.Hybrid && (_source as HybridDataSource)?.ImgSource == null))
        {
            WzDirectory root = _source.GetDirectory("Map");
            return (root?["WorldMap"] as WzDirectory)?[normalized] as WzImage;
        }
        WzImage image = _source.GetImage("Map", $"WorldMap/{normalized}");
        return image ?? _source.GetImageByPath($"Map/WorldMap/{normalized}");
    }

    public WzImage CreateBlank(string imageName)
    {
        if (!Capabilities.CanCreate) throw new InvalidOperationException("The active data source cannot create WorldMap images.");
        string normalized = imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName : imageName + ".img";
        return new WzImage(normalized);
    }

    public WorldMapBatchSaveResult SaveBatch(IEnumerable<WorldMapImageCandidate> candidates)
    {
        using IDisposable writeLease = Program.BeginRuntimeAssetWrite("save world-map assets");
        var list = (candidates ?? Enumerable.Empty<WorldMapImageCandidate>()).Where(c => c?.Image != null).ToList();
        var errors = new List<string>();
        var affected = new List<string>();
        foreach (WorldMapImageCandidate candidate in list)
        {
            string name = candidate.ImageName ?? candidate.Image.Name;
            string fileName = name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img";
            string relative = candidate.RelativePath ?? $"WorldMap/{fileName}";
            try
            {
                bool saved = Mode == WorldMapSourceMode.Wz || (Mode == WorldMapSourceMode.Hybrid && (_source as HybridDataSource)?.ImgSource == null)
                    ? StageWzImage(candidate.Image, fileName)
                    : _source.SaveImage("Map", candidate.Image, relative);
                if (!saved)
                    errors.Add($"Failed to save Map/{relative}.");
                else
                    affected.Add(relative.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                errors.Add($"Map/{relative}: {ex.Message}");
                break;
            }
        }
        return new WorldMapBatchSaveResult(errors.Count == 0, affected, errors, Mode);
    }

    private bool StageWzImage(WzImage candidate, string fileName)
    {
        WzDirectory root = _source.GetDirectory("Map");
        WzDirectory worldMap = root?["WorldMap"] as WzDirectory;
        if (worldMap == null) return false;
        WzImage existing = worldMap[fileName] as WzImage;
        if (existing != null) worldMap.RemoveImage(existing);
        candidate.Name = fileName;
        worldMap.AddImage(candidate);
        _source.MarkImageUpdated("Map", candidate);
        return true;
    }

    public bool StageDelete(string imageName, out string backupPath, out string error)
    {
        using IDisposable writeLease = Program.BeginRuntimeAssetWrite("delete world-map assets");
        backupPath = null;
        error = null;
        if (!Capabilities.CanDelete)
        {
            error = "The active source does not support recoverable WorldMap deletion.";
            return false;
        }
        if (Mode == WorldMapSourceMode.Wz)
        {
            WzImage image = Load(imageName);
            if (image?.Parent is WzDirectory owner)
            {
                owner.RemoveImage(image);
                image.Changed = true;
                _source.MarkImageUpdated("Map", image);
                return true;
            }
            error = "WorldMap image is not attached to a WZ directory.";
            return false;
        }

        string root = (_source as ImgFileSystemDataSource)?.Manager?.VersionPath
            ?? (_source as HybridDataSource)?.ImgSource?.Manager?.VersionPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            error = "IMG root path is unavailable.";
            return false;
        }
        string file = imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName : imageName + ".img";
        string full = Path.GetFullPath(Path.Combine(root, "Map", "WorldMap", file));
        string rootWithSep = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            error = "Resolved deletion path escapes the active IMG source.";
            return false;
        }
        if (!File.Exists(full))
        {
            error = $"WorldMap image does not exist: {file}";
            return false;
        }
        backupPath = full + ".worldmap-backup-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(full, backupPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void RestoreBackup(string backupPath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(destinationPath) || !File.Exists(backupPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        File.Move(backupPath, destinationPath);
    }
}
