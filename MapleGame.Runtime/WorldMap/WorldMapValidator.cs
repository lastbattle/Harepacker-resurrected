using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.WorldMap;

public enum WorldMapDiagnosticSeverity { Info, Warning, Error }

public sealed record WorldMapDiagnostic(WorldMapDiagnosticSeverity Severity, string Path, string Message)
{
    public bool IsError => Severity == WorldMapDiagnosticSeverity.Error;
}

public sealed class WorldMapValidationResult
{
    private readonly List<WorldMapDiagnostic> _diagnostics = new();
    public IReadOnlyList<WorldMapDiagnostic> Diagnostics => _diagnostics;
    public bool IsValid => !_diagnostics.Any(item => item.IsError);
    public IReadOnlyList<WorldMapDiagnostic> Errors => _diagnostics.Where(item => item.IsError).ToArray();
    public IReadOnlyList<WorldMapDiagnostic> Warnings => _diagnostics.Where(item => item.Severity == WorldMapDiagnosticSeverity.Warning).ToArray();
    public void Add(WorldMapDiagnosticSeverity severity, string path, string message) => _diagnostics.Add(new WorldMapDiagnostic(severity, path ?? string.Empty, message ?? string.Empty));
}

/// <summary>Optional source inventory used by the all-world-maps audit.</summary>
public sealed class WorldMapValidationContext
{
    public IReadOnlySet<int> ExistingMapIds { get; init; } = new HashSet<int>();
    public bool HasMapInventory { get; init; }
}

/// <summary>Native-shape diagnostics; validation never repairs or normalizes source data.</summary>
public static class WorldMapValidator
{
    public static WorldMapValidationResult Validate(WorldMapDocument document, WorldMapHierarchyIndex hierarchy = null, WorldMapMarkerRegistry markerRegistry = null, WorldMapValidationContext context = null)
    {
        var result = new WorldMapValidationResult();
        if (document == null) { result.Add(WorldMapDiagnosticSeverity.Error, string.Empty, "WorldMap document is null."); return result; }
        WorldMapSurface surface = document.Surface;
        if (surface == null) { result.Add(WorldMapDiagnosticSeverity.Error, document.ImageName, "WorldMap surface is null."); return result; }
        if (string.IsNullOrWhiteSpace(surface.LogicalName)) result.Add(WorldMapDiagnosticSeverity.Error, "info/WorldMap", "Logical surface name is missing.");
        ValidateCanvas(result, surface.BaseImage, "BaseImg/0", required: true);
        var markerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapMapEntry entry in surface.Entries)
        {
            string path = $"MapList/{entry.Key}";
            if (string.IsNullOrWhiteSpace(entry.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, "Marker key is empty.");
            else if (!markerKeys.Add(entry.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, $"Duplicate marker key '{entry.Key}'.");
            if (markerRegistry != null && !markerRegistry.Contains(entry.Type)) result.Add(WorldMapDiagnosticSeverity.Warning, path + "/type", $"Marker type {entry.Type} is not present in the active MapHelper registry.");
            if (markerRegistry?.TryGet(entry.Type, out WorldMapMarkerAsset asset) == true && !asset.IsKnown)
                result.Add(WorldMapDiagnosticSeverity.Error, path + "/type", $"Marker type {entry.Type} has an invalid source canvas.");
            ValidateCanvas(result, entry.Path, path + "/path", required: false);
            if (entry.MapIds.Count == 0) result.Add(WorldMapDiagnosticSeverity.Warning, path + "/mapNo", "Marker does not reference a map ID.");
            var local = new HashSet<int>();
            foreach (int mapId in entry.MapIds)
            {
                if (mapId <= 0 || mapId > 999999999) result.Add(WorldMapDiagnosticSeverity.Error, path + "/mapNo", $"Map ID {mapId} is outside the native 9-digit range.");
                if (!local.Add(mapId)) result.Add(WorldMapDiagnosticSeverity.Warning, path + "/mapNo", $"Duplicate map ID {mapId} is preserved.");
                if (context?.HasMapInventory == true && !context.ExistingMapIds.Contains(mapId))
                    result.Add(WorldMapDiagnosticSeverity.Error, path + "/mapNo", $"Referenced map ID {mapId} does not exist in the active map inventory.");
            }
        }
        var linkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapLink link in surface.Links)
        {
            string path = $"MapLink/{link.Key}";
            if (string.IsNullOrWhiteSpace(link.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, "Link key is empty.");
            else if (!linkKeys.Add(link.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, $"Duplicate link key '{link.Key}'.");
            if (string.IsNullOrWhiteSpace(link.LinkMap)) result.Add(WorldMapDiagnosticSeverity.Warning, path + "/link/linkMap", "Navigation link has no target surface.");
            ValidateCanvas(result, link.LinkImage, path + "/link/0", required: false);
        }
        var fogKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WorldMapFogLayer fog in surface.FogLayers)
        {
            string path = $"Fog/{fog.Key}";
            if (string.IsNullOrWhiteSpace(fog.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, "Fog key is empty.");
            else if (!fogKeys.Add(fog.Key)) result.Add(WorldMapDiagnosticSeverity.Error, path, $"Duplicate fog key '{fog.Key}'.");
            if (fog.Quest.HasValue != fog.QState.HasValue)
                result.Add(WorldMapDiagnosticSeverity.Warning, path, "Fog quest and qState should be supplied together.");
            if (fog.Quest < 0 || fog.QState < 0) result.Add(WorldMapDiagnosticSeverity.Error, path, "Fog quest and qState cannot be negative.");
            ValidateCanvas(result, fog.Image, path + "/0", required: true);
        }
        if (hierarchy != null)
        {
            if (hierarchy.GetDuplicateLogicalNames().Contains(surface.LogicalName, StringComparer.OrdinalIgnoreCase))
                result.Add(WorldMapDiagnosticSeverity.Error, "info/WorldMap", $"Logical surface name '{surface.LogicalName}' is duplicated.");
            if (!string.IsNullOrWhiteSpace(surface.ParentName) && hierarchy.Find(surface.ParentName) == null)
                result.Add(WorldMapDiagnosticSeverity.Warning, "info/parentMap", $"Parent surface '{surface.ParentName}' is missing.");
            foreach (WorldMapLink link in surface.Links)
                if (!string.IsNullOrWhiteSpace(link.LinkMap) && hierarchy.Find(link.LinkMap) == null)
                    result.Add(WorldMapDiagnosticSeverity.Warning, $"MapLink/{link.Key}/link/linkMap", $"Link target '{link.LinkMap}' is missing.");
            if (hierarchy.HasCycles) result.Add(WorldMapDiagnosticSeverity.Error, "hierarchy", "WorldMap parent hierarchy contains a cycle.");
        }
        return result;
    }

    private static void ValidateCanvas(WorldMapValidationResult result, WorldMapCanvasRef canvas, string path, bool required)
    {
        if (canvas == null) { if (required) result.Add(WorldMapDiagnosticSeverity.Warning, path, "Canvas is missing."); return; }
        if (canvas.Width <= 0 || canvas.Height <= 0) result.Add(WorldMapDiagnosticSeverity.Error, path, $"Canvas dimensions {canvas.Width}x{canvas.Height} are invalid.");
        if (canvas.RawProperty == null || canvas.RawProperty.PngProperty == null)
            result.Add(WorldMapDiagnosticSeverity.Error, path, "Canvas has no decoded image data.");
    }

    public static WorldMapValidationResult ValidateDocument(WorldMapDocument document, WorldMapHierarchyIndex hierarchy = null, WorldMapMarkerRegistry markerRegistry = null) => Validate(document, hierarchy, markerRegistry);
    public static WorldMapValidationResult ValidateAll(IEnumerable<WorldMapDocument> documents, WorldMapMarkerRegistry markerRegistry = null, WorldMapValidationContext context = null)
    {
        WorldMapDocument[] values = (documents ?? Enumerable.Empty<WorldMapDocument>()).Where(document => document != null).ToArray();
        var hierarchy = new WorldMapHierarchyIndex(values);
        var result = new WorldMapValidationResult();
        foreach (WorldMapDocument document in values)
        {
            WorldMapValidationResult current = Validate(document, hierarchy, markerRegistry, context);
            foreach (WorldMapDiagnostic diagnostic in current.Diagnostics) result.Add(diagnostic.Severity, diagnostic.Path, diagnostic.Message);
        }
        return result;
    }
}
