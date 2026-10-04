using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HaCreator.MapSimulator.Assets;
using MapleLib.WzLib;

namespace HaCreator.MapSimulator.WorldMap;

/// <summary>A non-fatal problem encountered while reading the native World Map catalog.</summary>
public sealed record WorldMapReadDiagnostic(string ImageName, string Message);

/// <summary>Detached World Map documents and diagnostics produced by a source read.</summary>
public sealed record WorldMapReadResult(
    IReadOnlyList<WorldMapDocument> Documents,
    IReadOnlyList<WorldMapReadDiagnostic> Diagnostics);

/// <summary>
/// Reads native Map/WorldMap images through the runtime asset boundary. The reader
/// borrows the supplied source and never owns or disposes it; decoded documents are
/// detached by <see cref="WorldMapCodec"/> before they are returned.
/// </summary>
public sealed class WorldMapSourceReader
{
    private readonly IRuntimeAssetSource _source;

    public WorldMapSourceReader(IRuntimeAssetSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public WorldMapReadResult ReadDocuments(CancellationToken cancellationToken = default)
    {
        var documents = new List<WorldMapDocument>();
        var diagnostics = new List<WorldMapReadDiagnostic>();
        IEnumerable<string> names = _source.GetImageNamesInDirectory("Map", "WorldMap")
            ?? Array.Empty<string>();

        foreach (string rawName in names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imageName = NormalizeImageName(rawName);
            if (WorldMapExclusionList.IsExclusionImage(imageName))
                continue;

            WzImage image = _source.FindImage("Map", $"WorldMap/{imageName}.img");
            if (image == null)
            {
                diagnostics.Add(new WorldMapReadDiagnostic(
                    imageName,
                    $"World Map image was listed by the source but could not be loaded: {imageName}.img"));
                continue;
            }

            try
            {
                documents.Add(WorldMapCodec.Read(image));
            }
            catch (Exception exception)
            {
                diagnostics.Add(new WorldMapReadDiagnostic(
                    imageName,
                    $"World Map image could not be decoded: {exception.Message}"));
            }
        }

        return new WorldMapReadResult(documents, diagnostics);
    }

    private static string NormalizeImageName(string imageName)
    {
        string normalized = imageName.Trim().Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        if (separator >= 0)
            normalized = normalized[(separator + 1)..];
        if (normalized.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        return normalized;
    }
}
