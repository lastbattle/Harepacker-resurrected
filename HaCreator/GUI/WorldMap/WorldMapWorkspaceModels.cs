using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;

namespace HaCreator.GUI.WorldMap;

/// <summary>Presentation-only surface row used by the WPF shell.</summary>
public sealed class WorldMapSurfaceItem : NotifyPropertyChangedBase
{
    private bool _isDirty;
    private string _logicalName = string.Empty;
    private string _parentName = string.Empty;
    private int _diagnosticsCount;

    public string ImageName { get; init; } = string.Empty;
    public string ImagePath => string.IsNullOrWhiteSpace(ImageName) ? string.Empty : $"Map/WorldMap/{ImageName}.img";
    public string LogicalName { get => _logicalName; set => Set(ref _logicalName, value); }
    public string ParentName { get => _parentName; set => Set(ref _parentName, value); }
    public int BaseWidth { get; set; } = 640;
    public int BaseHeight { get; set; } = 470;
    public int BaseOriginX { get; set; } = 320;
    public int BaseOriginY { get; set; } = 235;
    public int MarkerCount => Markers.Count;
    public int MapCount => Markers.Sum(marker => marker.MapIds.Count);
    public int LinkCount { get; set; }
    public int FogCount { get; set; }
    public int DiagnosticsCount { get => _diagnosticsCount; set => Set(ref _diagnosticsCount, value); }
    public bool IsDirty { get => _isDirty; set => Set(ref _isDirty, value); }
    public ObservableCollection<WorldMapMarkerItem> Markers { get; } = new();
    public ObservableCollection<WorldMapLinkItem> Links { get; } = new();
    public ObservableCollection<WorldMapFogItem> FogLayers { get; } = new();
    public ObservableCollection<WorldMapDiagnosticItem> Diagnostics { get; } = new();

    public string MarkerSummary => $"{MarkerCount} markers · {MapCount} maps";
    public string RelationshipSummary => $"{LinkCount} links · {FogCount} fog";

    public WorldMapSurfaceItem()
    {
        Markers.CollectionChanged += (_, _) => Raise(nameof(MarkerCount), nameof(MapCount), nameof(MarkerSummary));
        Links.CollectionChanged += (_, _) =>
        {
            LinkCount = Links.Count;
            Raise(nameof(LinkCount), nameof(RelationshipSummary));
        };
        FogLayers.CollectionChanged += (_, _) =>
        {
            FogCount = FogLayers.Count;
            Raise(nameof(FogCount), nameof(RelationshipSummary));
        };
    }
}

/// <summary>Presentation-only map marker. Native key and map IDs are retained for lossless editing.</summary>
public sealed class WorldMapMarkerItem : NotifyPropertyChangedBase
{
    private int _markerType;
    private int _x;
    private int _y;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _townDescription = string.Empty;
    private string _mapIdsText = string.Empty;
    private bool _isSelected;
    private ImageSource? _markerImage;
    private int _originX;
    private int _originY;

    public string NativeKey { get; init; } = string.Empty;
    public int MarkerType { get => _markerType; set => Set(ref _markerType, value); }
    public int X { get => _x; set { if (Set(ref _x, value)) Raise(nameof(CanvasX)); } }
    public int Y { get => _y; set { if (Set(ref _y, value)) Raise(nameof(CanvasY)); } }
    public string Title { get => _title; set { if (Set(ref _title, value)) Raise(nameof(DisplayName)); } }
    public string Description { get => _description; set => Set(ref _description, value); }
    public string TownDescription { get => _townDescription; set => Set(ref _townDescription, value); }
    public ImageSource? MarkerImage { get => _markerImage; set => Set(ref _markerImage, value); }
    public double CanvasX => _originX + X;
    public double CanvasY => _originY + Y;
    public ObservableCollection<int> MapIds { get; } = new();

    public string MapIdsText
    {
        get => _mapIdsText;
        set
        {
            if (!Set(ref _mapIdsText, value))
                return;
            ParseMapIds(value);
            Raise(nameof(MapCount));
        }
    }

    public int MapCount => MapIds.Count;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? $"Marker {NativeKey}" : Title;

    public WorldMapMarkerItem()
    {
        MapIds.CollectionChanged += (_, _) => Raise(nameof(MapCount));
    }

    public void SetCanvasOrigin(int originX, int originY)
    {
        if (_originX == originX && _originY == originY)
            return;
        _originX = originX;
        _originY = originY;
        Raise(nameof(CanvasX), nameof(CanvasY));
    }

    public void SetMapIds(System.Collections.Generic.IEnumerable<int> ids, bool preserveDuplicates = false)
    {
        MapIds.Clear();
        IEnumerable<int> values = (ids ?? Enumerable.Empty<int>()).Where(id => id > 0);
        if (!preserveDuplicates)
            values = values.Distinct();
        foreach (int id in values)
            MapIds.Add(id);
        _mapIdsText = string.Join(", ", MapIds);
        Raise(nameof(MapIdsText), nameof(MapCount));
    }

    public bool AddMapId(int mapId)
    {
        if (mapId <= 0 || MapIds.Contains(mapId))
            return false;
        MapIds.Add(mapId);
        SyncMapIdsText();
        return true;
    }

    public bool RemoveMapId(int mapId)
    {
        if (!MapIds.Remove(mapId))
            return false;
        SyncMapIdsText();
        return true;
    }

    private void ParseMapIds(string value)
    {
        int[] ids = (value ?? string.Empty)
            .Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.SequenceEqual(MapIds))
            return;
        MapIds.Clear();
        foreach (int id in ids)
            MapIds.Add(id);
    }

    private void SyncMapIdsText()
    {
        _mapIdsText = string.Join(", ", MapIds);
        Raise(nameof(MapIdsText), nameof(MapCount));
    }
}

public sealed class WorldMapLinkItem : NotifyPropertyChangedBase
{
    public string NativeKey { get; init; } = string.Empty;
    private string _targetName = string.Empty;
    private string _tooltip = string.Empty;
    private int _x;
    private int _y;
    public string TargetName { get => _targetName; set => Set(ref _targetName, value); }
    public string Tooltip { get => _tooltip; set => Set(ref _tooltip, value); }
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public string DisplayName => string.IsNullOrWhiteSpace(Tooltip) ? TargetName : Tooltip;
}

public sealed class WorldMapFogItem : NotifyPropertyChangedBase
{
    public string NativeKey { get; init; } = string.Empty;
    private int? _quest;
    private int? _qState;
    public int? Quest { get => _quest; set => Set(ref _quest, value); }
    public int? QState { get => _qState; set => Set(ref _qState, value); }
    public string DisplayName => $"{NativeKey}  (quest {Quest?.ToString(CultureInfo.InvariantCulture) ?? "-"}, qState {QState?.ToString(CultureInfo.InvariantCulture) ?? "-"})";
}

public sealed class WorldMapDiagnosticItem
{
    public string Severity { get; init; } = "Info";
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string DisplayText => string.IsNullOrWhiteSpace(Path) ? $"{Severity}: {Message}" : $"{Severity}: {Message}  ({Path})";
}

public sealed class WorldMapReviewChangeItem
{
    public string ImagePath { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Before { get; init; } = string.Empty;
    public string After { get; init; } = string.Empty;
    public string DisplayText => string.IsNullOrWhiteSpace(ImagePath)
        ? $"{Path}: {Before} -> {After}"
        : $"{ImagePath}  |  {Path}: {Before} -> {After}";
}

/// <summary>
/// WZ-backed marker choices. Legacy MapHelper.img publishes only numbered
/// canvases, so the labels describe the actual icon rather than inventing a
/// gameplay meaning. Unknown values remain selectable and round-trip intact.
/// </summary>
public sealed record WorldMapMarkerTypeOption(int Value, string DisplayName)
{
    public string DisplayText => $"{Value} — {DisplayName}";

    public static WorldMapMarkerTypeOption FromValue(int value)
    {
        string key = value is >= 0 and <= 7 ? $"MarkerType{value}" : "MarkerTypeSourceDefined";
        return new WorldMapMarkerTypeOption(value, WorldMapEditorTextExtension.Get(key));
    }
}

public sealed class WorldMapMapSearchItem
{
    public int MapId { get; init; }
    public string StreetName { get; init; } = string.Empty;
    public string MapName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string DisplayText => $"{MapId.ToString(CultureInfo.InvariantCulture)}  {MapName}  {StreetName}".Trim();
}

/// <summary>Resolves a native map ID to the human-readable catalog names used by the inspector.</summary>
public sealed class WorldMapMapReferenceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId))
            return string.Empty;
        Tuple<string, string, string>? names = Program.InfoManager?.MapsNameCache?.GetValueOrDefault(mapId.ToString(CultureInfo.InvariantCulture));
        string mapName = names?.Item2?.Trim() ?? string.Empty;
        string streetName = names?.Item1?.Trim() ?? string.Empty;
        return parameter?.ToString() switch
        {
            "MapName" => string.IsNullOrWhiteSpace(mapName) ? WorldMapEditorTextExtension.Get("UnknownMap") : mapName,
            "StreetName" => streetName,
            "ToolTip" => string.Join(" · ", new[] { mapName, streetName }.Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => mapName,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class WorldMapAvailabilityItem
{
    public int MapId { get; init; }
    public string MapName { get; init; } = string.Empty;
    public string StreetName { get; init; } = string.Empty;
    public string NpcSummary { get; init; } = string.Empty;
    public string MobSummary { get; init; } = string.Empty;
    public string DiagnosticSummary { get; init; } = string.Empty;
    public string DisplayText => $"{MapId.ToString(CultureInfo.InvariantCulture)}  {MapName}  NPC: {NpcSummary}  Mob: {MobSummary}";
}

public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;
        field = value;
        Raise(propertyName);
        return true;
    }

    protected void Raise(params string?[] propertyNames)
    {
        foreach (string? propertyName in propertyNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>View model for the World Map shell. It intentionally contains no WPF controls.</summary>
public sealed class WorldMapWorkspaceViewModel : NotifyPropertyChangedBase
{
    private string _sourceName = string.Empty;
    private string _sourceMode = string.Empty;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private string _imagePath = string.Empty;
    private bool _isDirty;
    private bool _isLoading;
    private bool _showGrid = true;
    private bool _showRulers;
    private bool _showLabels = true;
    private bool _showLinks = true;
    private bool _showFog = true;
    private bool _showDerivedOverlays = true;
    private bool _showRawBounds;
    private double _zoom = 1;
    private WorldMapSurfaceItem? _selectedSurface;
    private WorldMapMarkerItem? _selectedMarker;
    private WorldMapLinkItem? _selectedLink;
    private WorldMapFogItem? _selectedFog;
    private WorldMapMapSearchItem? _selectedMapSearchItem;
    private string _mapSearchText = string.Empty;

    public ObservableCollection<WorldMapSurfaceItem> Surfaces { get; } = new();
    public ObservableCollection<WorldMapDiagnosticItem> Diagnostics { get; } = new();
    public ObservableCollection<WorldMapReviewChangeItem> ReviewChanges { get; } = new();
    public ObservableCollection<WorldMapAvailabilityItem> DerivedAvailability { get; } = new();
    public ObservableCollection<WorldMapMapSearchItem> MapSearchResults { get; } = new();
    public ObservableCollection<WorldMapMarkerTypeOption> MarkerTypes { get; } = new();
    public ICollectionView FilteredSurfaces { get; }

    public string SourceName { get => _sourceName; set => Set(ref _sourceName, value); }
    public string SourceMode { get => _sourceMode; set => Set(ref _sourceMode, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value))
                return;
            FilteredSurfaces.Refresh();
        }
    }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string ImagePath { get => _imagePath; set => Set(ref _imagePath, value); }
    public bool IsDirty { get => _isDirty; set { if (Set(ref _isDirty, value)) Raise(nameof(DirtySummary)); } }
    public bool IsLoading { get => _isLoading; set => Set(ref _isLoading, value); }
    public bool ShowGrid { get => _showGrid; set => Set(ref _showGrid, value); }
    public bool ShowRulers { get => _showRulers; set => Set(ref _showRulers, value); }
    public bool ShowLabels { get => _showLabels; set => Set(ref _showLabels, value); }
    public bool ShowLinks { get => _showLinks; set => Set(ref _showLinks, value); }
    public bool ShowFog { get => _showFog; set => Set(ref _showFog, value); }
    public bool ShowDerivedOverlays { get => _showDerivedOverlays; set => Set(ref _showDerivedOverlays, value); }
    public bool ShowRawBounds { get => _showRawBounds; set => Set(ref _showRawBounds, value); }
    public double Zoom { get => _zoom; set => Set(ref _zoom, Math.Clamp(value, 0.2, 4)); }
    public WorldMapSurfaceItem? SelectedSurface
    {
        get => _selectedSurface;
        set
        {
            if (!Set(ref _selectedSurface, value))
                return;
            SelectedMarker = value?.Markers.FirstOrDefault();
            ImagePath = value?.ImagePath ?? string.Empty;
            Raise(nameof(SelectedSurface), nameof(SelectedMarker));
        }
    }
    public WorldMapMarkerItem? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker != null)
                _selectedMarker.IsSelected = false;
            if (Set(ref _selectedMarker, value))
                Raise(nameof(SelectedMarker));
            if (_selectedMarker != null)
                _selectedMarker.IsSelected = true;
        }
    }
    public WorldMapLinkItem? SelectedLink { get => _selectedLink; set => Set(ref _selectedLink, value); }
    public WorldMapFogItem? SelectedFog { get => _selectedFog; set => Set(ref _selectedFog, value); }
    public WorldMapMapSearchItem? SelectedMapSearchItem { get => _selectedMapSearchItem; set => Set(ref _selectedMapSearchItem, value); }
    public string MapSearchText
    {
        get => _mapSearchText;
        set => Set(ref _mapSearchText, value);
    }
    public string DirtySummary => IsDirty ? "● unsaved changes" : "clean";
    public string ValidationSummary => Diagnostics.Count == 0 ? "valid" : $"{Diagnostics.Count} diagnostic(s)";

    public void SetMarkerTypes(IEnumerable<int> types)
    {
        MarkerTypes.Clear();
        foreach (int type in (types ?? Enumerable.Empty<int>()).Distinct().OrderBy(type => type))
            MarkerTypes.Add(WorldMapMarkerTypeOption.FromValue(type));
    }

    public WorldMapWorkspaceViewModel()
    {
        FilteredSurfaces = CollectionViewSource.GetDefaultView(Surfaces);
        FilteredSurfaces.Filter = FilterSurface;
        Surfaces.CollectionChanged += (_, _) => Raise(nameof(ValidationSummary));
        Diagnostics.CollectionChanged += (_, _) => Raise(nameof(ValidationSummary));
    }

    private bool FilterSurface(object value)
    {
        if (value is not WorldMapSurfaceItem surface)
            return false;
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        string query = SearchText.Trim();
        return surface.ImageName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || surface.LogicalName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || surface.ParentName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || surface.Markers.Any(marker => marker.MapIds.Any(id => id.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase))
                || marker.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
