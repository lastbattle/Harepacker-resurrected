#nullable enable

using HaCreator.GUI.Localization;
using HaCreator.GUI.InstanceEditor;
using HaCreator.GUI.EditorPanels;
using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using HaCreator.Wz;
using MapleLib.WzLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.GUI.WorldMap;

/// <summary>
/// Native WPF shell for authoring Map/WorldMap images.  The document and
/// transaction services live in HaCreator.WorldMap; this window keeps a
/// presentation model so the shell remains usable when an older source does
/// not expose those services yet.  Unsupported operations are reported in the
/// persistent status/diagnostics area instead of mutating files ad hoc.
/// </summary>
public partial class WorldMapWorkspace : Window
{
    public static readonly RoutedCommand NewSurfaceCommand = CreateCommand(nameof(NewSurfaceCommand));
    public static readonly RoutedCommand DuplicateSurfaceCommand = CreateCommand(nameof(DuplicateSurfaceCommand));
    public static readonly RoutedCommand OpenReloadCommand = CreateCommand(nameof(OpenReloadCommand));
    public static readonly RoutedCommand SaveSelectedCommand = CreateCommand(nameof(SaveSelectedCommand));
    public static readonly RoutedCommand SaveAllCommand = CreateCommand(nameof(SaveAllCommand));
    public static readonly RoutedCommand AddMarkerCommand = CreateCommand(nameof(AddMarkerCommand));
    public static readonly RoutedCommand AddLinkCommand = CreateCommand(nameof(AddLinkCommand));
    public static readonly RoutedCommand AddFogCommand = CreateCommand(nameof(AddFogCommand));
    public static readonly RoutedCommand RemoveMarkerCommand = CreateCommand(nameof(RemoveMarkerCommand));
    public static readonly RoutedCommand RemoveLinkCommand = CreateCommand(nameof(RemoveLinkCommand));
    public static readonly RoutedCommand RemoveFogCommand = CreateCommand(nameof(RemoveFogCommand));
    public static readonly RoutedCommand AddMapToMarkerCommand = CreateCommand(nameof(AddMapToMarkerCommand));
    public static readonly RoutedCommand CreateChildCommand = CreateCommand(nameof(CreateChildCommand));
    public static readonly RoutedCommand RemoveFromHierarchyCommand = CreateCommand(nameof(RemoveFromHierarchyCommand));
    public static readonly RoutedCommand DeleteAssetCommand = CreateCommand(nameof(DeleteAssetCommand));
    public static readonly RoutedCommand ValidateSelectedCommand = CreateCommand(nameof(ValidateSelectedCommand));
    public static readonly RoutedCommand ValidateAllCommand = CreateCommand(nameof(ValidateAllCommand));
    public static readonly RoutedCommand ReplaceBackgroundCommand = CreateCommand(nameof(ReplaceBackgroundCommand));
    public static readonly RoutedCommand ReviewChangesCommand = CreateCommand(nameof(ReviewChangesCommand));
    public static readonly RoutedCommand FitCanvasCommand = CreateCommand(nameof(FitCanvasCommand));
    public static readonly RoutedCommand ZoomOutCommand = CreateCommand(nameof(ZoomOutCommand));
    public static readonly RoutedCommand ZoomInCommand = CreateCommand(nameof(ZoomInCommand));
    public static readonly RoutedCommand RuntimePreviewCommand = CreateCommand(nameof(RuntimePreviewCommand));

    private readonly WorldMapWorkspaceViewModel viewModel = new();
    private readonly Dictionary<string, WorldMapDocument> coreDocuments = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorldMapEditSession> editSessions = new(StringComparer.OrdinalIgnoreCase);
    private WorldMapPreviewCache? previewCache;
    private WorldMapAvailabilityIndex? availabilityIndex;
    private WorldMapTransactionService? transactionService;
    private HotSwapRefreshService? hotSwapService;
    private readonly CancellationTokenSource availabilityCancellation = new();
    private CancellationTokenSource availabilityScanCancellation = new();
    private readonly Dictionary<string, string> logicalNameSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> parentNameSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private int pendingMapSelection;
    private WorldMapMarkerRegistry markerRegistry = new();
    private bool catalogLoaded;
    private bool placingMarker;
    private bool suppressDirty;
    private bool autoFitCanvas = true;
    private WorldMapMarkerItem? draggedMarker;
    private FrameworkElement? markerDragElement;
    private Point markerDragStartPoint;
    private int markerDragStartX;
    private int markerDragStartY;
    private bool markerDragMoved;

    public WorldMapWorkspace()
    {
        InitializeComponent();
        DataContext = viewModel;

        CommandBindings.Add(new CommandBinding(NewSurfaceCommand, (_, _) => NewSurface()));
        CommandBindings.Add(new CommandBinding(DuplicateSurfaceCommand, (_, _) => DuplicateSurface(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(OpenReloadCommand, (_, _) => LoadCatalog()));
        CommandBindings.Add(new CommandBinding(SaveSelectedCommand, (_, _) => SaveSelected(), (_, e) => e.CanExecute = viewModel.SelectedSurface?.IsDirty == true));
        CommandBindings.Add(new CommandBinding(SaveAllCommand, (_, _) => SaveAll(), (_, e) => e.CanExecute = viewModel.IsDirty));
        CommandBindings.Add(new CommandBinding(AddMarkerCommand, (_, _) => BeginAddMarker(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(AddLinkCommand, (_, _) => AddLink(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(AddFogCommand, (_, _) => AddFog(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(RemoveMarkerCommand, (_, _) => RemoveMarker(), (_, e) => e.CanExecute = viewModel.SelectedMarker != null));
        CommandBindings.Add(new CommandBinding(RemoveLinkCommand, (_, _) => RemoveLink(), (_, e) => e.CanExecute = viewModel.SelectedLink != null));
        CommandBindings.Add(new CommandBinding(RemoveFogCommand, (_, _) => RemoveFog(), (_, e) => e.CanExecute = viewModel.SelectedFog != null));
        CommandBindings.Add(new CommandBinding(AddMapToMarkerCommand, (_, _) => AddMapToMarker(), (_, e) => e.CanExecute = viewModel.SelectedMarker != null && viewModel.SelectedMapSearchItem != null));
        CommandBindings.Add(new CommandBinding(CreateChildCommand, (_, _) => CreateChild(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(RemoveFromHierarchyCommand, (_, _) => RemoveFromHierarchy(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(DeleteAssetCommand, (_, _) => DeleteAsset(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(ValidateSelectedCommand, (_, _) => ValidateSelected(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(ValidateAllCommand, (_, _) => ValidateAll()));
        CommandBindings.Add(new CommandBinding(ReplaceBackgroundCommand, (_, _) => ReplaceBackground(), (_, e) => e.CanExecute = viewModel.SelectedSurface != null));
        CommandBindings.Add(new CommandBinding(ReviewChangesCommand, (_, _) => ReviewChanges()));
        CommandBindings.Add(new CommandBinding(FitCanvasCommand, (_, _) => FitCanvas()));
        CommandBindings.Add(new CommandBinding(ZoomOutCommand, (_, _) => ZoomBy(-0.1)));
        CommandBindings.Add(new CommandBinding(ZoomInCommand, (_, _) => ZoomBy(0.1)));
        CommandBindings.Add(new CommandBinding(RuntimePreviewCommand, (_, _) => RuntimePreview()));

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += Workspace_Loaded;
        Closing += Workspace_Closing;
    }

    private static RoutedCommand CreateCommand(string name) => new(name, typeof(WorldMapWorkspace));

    /// <summary>Selects the first native marker containing a map ID. Requests made
    /// before catalog load are replayed after the source has been indexed.</summary>
    public void SelectMap(int mapId)
    {
        if (mapId <= 0)
            return;
        if (!catalogLoaded)
        {
            pendingMapSelection = mapId;
            viewModel.StatusText = WorldMapEditorTextExtension.Format("MapSelectionLoading", mapId);
            return;
        }

        var matches = viewModel.Surfaces
            .SelectMany(surface => surface.Markers
                .Where(marker => marker.MapIds.Contains(mapId))
                .Select(marker => (Surface: surface, Marker: marker)))
            .ToArray();
        if (matches.Length == 0)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Format("MapSelectionMissing", mapId);
            AddDiagnostic("Warning", $"mapNo/{mapId}", viewModel.StatusText);
            return;
        }

        viewModel.SelectedSurface = matches[0].Surface;
        viewModel.SelectedMarker = matches[0].Marker;
        surfaceList?.ScrollIntoView(matches[0].Surface);
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MapSelectionFound", mapId, matches.Length);
    }

    private void AlwaysCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void NewSurface_Executed(object sender, ExecutedRoutedEventArgs e) => NewSurface();
    private void DuplicateSurface_Executed(object sender, ExecutedRoutedEventArgs e) => DuplicateSurface();
    private void OpenReload_Executed(object sender, ExecutedRoutedEventArgs e) => LoadCatalog();
    private void SaveSelected_Executed(object sender, ExecutedRoutedEventArgs e) => SaveSelected();
    private void SaveAll_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAll();
    private void AddMarker_Executed(object sender, ExecutedRoutedEventArgs e) => BeginAddMarker();
    private void AddLink_Executed(object sender, ExecutedRoutedEventArgs e) => AddLink();
    private void AddFog_Executed(object sender, ExecutedRoutedEventArgs e) => AddFog();
    private void RemoveMarker_Executed(object sender, ExecutedRoutedEventArgs e) => RemoveMarker();
    private void RemoveLink_Executed(object sender, ExecutedRoutedEventArgs e) => RemoveLink();
    private void RemoveFog_Executed(object sender, ExecutedRoutedEventArgs e) => RemoveFog();
    private void AddMapToMarker_Executed(object sender, ExecutedRoutedEventArgs e) => AddMapToMarker();
    private void CreateChild_Executed(object sender, ExecutedRoutedEventArgs e) => CreateChild();
    private void RemoveFromHierarchy_Executed(object sender, ExecutedRoutedEventArgs e) => RemoveFromHierarchy();
    private void DeleteAsset_Executed(object sender, ExecutedRoutedEventArgs e) => DeleteAsset();
    private void ValidateSelected_Executed(object sender, ExecutedRoutedEventArgs e) => ValidateSelected();
    private void ValidateAll_Executed(object sender, ExecutedRoutedEventArgs e) => ValidateAll();
    private void ReplaceBackground_Executed(object sender, ExecutedRoutedEventArgs e) => ReplaceBackground();
    private void ReviewChanges_Executed(object sender, ExecutedRoutedEventArgs e) => ReviewChanges();
    private void FitCanvas_Executed(object sender, ExecutedRoutedEventArgs e) => FitCanvas();
    private void ZoomOut_Executed(object sender, ExecutedRoutedEventArgs e) => ZoomBy(-0.1);
    private void ZoomIn_Executed(object sender, ExecutedRoutedEventArgs e) => ZoomBy(0.1);
    private void RuntimePreview_Executed(object sender, ExecutedRoutedEventArgs e) => RuntimePreview();
    private void Undo_Executed(object sender, ExecutedRoutedEventArgs e) => Undo();
    private void Redo_Executed(object sender, ExecutedRoutedEventArgs e) => Redo();

    private void AISettings_Click(object sender, RoutedEventArgs e)
    {
        new AISettingsDialog { Owner = this }.ShowDialog();
    }

    private void Undo()
    {
        if (!TryGetSelectedSession(out WorldMapEditSession? session, out WorldMapSurfaceItem? surface) || !session.Undo())
            return;
        suppressDirty = true;
        WorldMapWorkspaceSource.ApplyCoreDocument(surface, session.Document);
        suppressDirty = false;
        surface.IsDirty = session.IsDirty;
        viewModel.IsDirty = coreDocuments.Values.Any(document => document.IsDirty);
        viewModel.StatusText = WorldMapEditorTextExtension.Get("UndoComplete");
        viewModel.SelectedMarker = surface.Markers.FirstOrDefault();
        RenderCanvas();
    }

    private void Redo()
    {
        if (!TryGetSelectedSession(out WorldMapEditSession? session, out WorldMapSurfaceItem? surface) || !session.Redo())
            return;
        suppressDirty = true;
        WorldMapWorkspaceSource.ApplyCoreDocument(surface, session.Document);
        suppressDirty = false;
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        viewModel.StatusText = WorldMapEditorTextExtension.Get("RedoComplete");
        viewModel.SelectedMarker = surface.Markers.FirstOrDefault();
        RenderCanvas();
    }

    private bool TryGetSelectedSession(out WorldMapEditSession? session, out WorldMapSurfaceItem? surface)
    {
        surface = viewModel.SelectedSurface;
        session = null;
        return surface != null && editSessions.TryGetValue(surface.ImageName, out session);
    }

    private async void Workspace_Loaded(object sender, RoutedEventArgs e)
    {
        if (catalogLoaded)
            return;
        await LoadCatalogAsync();
    }

    private async void LoadCatalog()
    {
        await LoadCatalogAsync();
    }

    private async Task LoadCatalogAsync()
    {
        viewModel.IsLoading = true;
        viewModel.StatusText = WorldMapEditorTextExtension.Get("CatalogLoading");
        try
        {
            WorldMapCatalogSnapshot snapshot = await Task.Run(WorldMapWorkspaceSource.ReadCatalog);
            suppressDirty = true;
            coreDocuments.Clear();
            editSessions.Clear();
            previewCache?.Dispose();
            previewCache = new WorldMapPreviewCache();
            availabilityIndex?.Dispose();
            availabilityIndex = Program.DataSource == null ? null : new WorldMapAvailabilityIndex(
                Program.DataSource, new EditorWorldMapAvailabilityLookup(Program.InfoManager));
            viewModel.Surfaces.Clear();
            viewModel.Diagnostics.Clear();
            foreach (WorldMapSurfaceItem surface in snapshot.Surfaces)
            {
                viewModel.Surfaces.Add(surface);
                if (surface.Diagnostics.Count > 0)
                {
                    foreach (WorldMapDiagnosticItem diagnostic in surface.Diagnostics)
                        viewModel.Diagnostics.Add(diagnostic);
                }
            }
            foreach ((string key, WorldMapDocument document) in snapshot.CoreDocuments)
            {
                coreDocuments[key] = document;
                editSessions[key] = new WorldMapEditSession(document);
            }
            markerRegistry = WorldMapWorkspaceSource.ReadMarkerRegistry();
            viewModel.SetMarkerTypes(markerRegistry.Types
                .Concat(snapshot.CoreDocuments.Values.SelectMany(document => document.Surface.Entries.Select(entry => entry.Type)))
                .DefaultIfEmpty(0));
            transactionService = Program.DataSource == null
                ? null
                : new WorldMapTransactionService(new WorldMapSourceOperations(Program.DataSource), coreDocuments.Values, markerRegistry);
            logicalNameSnapshots.Clear();
            parentNameSnapshots.Clear();
            foreach (WorldMapSurfaceItem surface in viewModel.Surfaces)
                AttachSurfaceHandlers(surface);
            viewModel.SourceName = snapshot.SourceName;
            viewModel.SourceMode = snapshot.SourceMode;
            viewModel.IsDirty = false;
            viewModel.StatusText = snapshot.Surfaces.Count == 0
                ? WorldMapEditorTextExtension.Get("CatalogEmpty")
                : WorldMapEditorTextExtension.Format("Ready", snapshot.Surfaces.Count);
            catalogLoaded = true;
            SubscribeToHotSwap();
            viewModel.SelectedSurface = viewModel.Surfaces.FirstOrDefault();
            if (pendingMapSelection > 0)
            {
                int mapId = pendingMapSelection;
                pendingMapSelection = 0;
                SelectMap(mapId);
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationFailed", exception.Message);
            AddDiagnostic("Error", "Catalog", exception.Message);
        }
        finally
        {
            suppressDirty = false;
            viewModel.IsLoading = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorldMapWorkspaceViewModel.SelectedSurface))
        {
            CancelMarkerPlacement(updateStatus: false);
            CancelMarkerDrag(revert: true, updateStatus: false);
            AttachMarkerHandlers(viewModel.SelectedSurface);
            RenderCanvas();
            if (autoFitCanvas)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyFitCanvasZoom);
        }
        else if (e.PropertyName == nameof(WorldMapWorkspaceViewModel.SelectedMarker))
        {
            AttachMarkerHandlers(viewModel.SelectedMarker);
            _ = LoadAvailabilityAsync();
        }
        else if (e.PropertyName == nameof(WorldMapWorkspaceViewModel.MapSearchText))
            RefreshMapSearch();
        CommandManager.InvalidateRequerySuggested();
    }

    private void AttachMarkerHandlers(object? item)
    {
        if (item is WorldMapMarkerItem marker)
        {
            marker.PropertyChanged -= Marker_PropertyChanged;
            marker.PropertyChanged += Marker_PropertyChanged;
        }
    }

    private void AttachMarkerHandlers(WorldMapSurfaceItem? surface)
    {
        if (surface == null)
            return;
        foreach (WorldMapMarkerItem marker in surface.Markers)
            AttachMarkerHandlers(marker);
    }

    private void AttachSurfaceHandlers(WorldMapSurfaceItem surface)
    {
        if (surface == null)
            return;
        surface.PropertyChanged -= Surface_PropertyChanged;
        surface.PropertyChanged += Surface_PropertyChanged;
        logicalNameSnapshots[surface.ImageName] = surface.LogicalName;
        parentNameSnapshots[surface.ImageName] = surface.ParentName;
        AttachMarkerHandlers(surface);
        foreach (WorldMapLinkItem link in surface.Links)
        {
            link.PropertyChanged -= Link_PropertyChanged;
            link.PropertyChanged += Link_PropertyChanged;
        }
        foreach (WorldMapFogItem fog in surface.FogLayers)
        {
            fog.PropertyChanged -= Fog_PropertyChanged;
            fog.PropertyChanged += Fog_PropertyChanged;
        }
    }

    private void Surface_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (suppressDirty || sender is not WorldMapSurfaceItem surface ||
            (e.PropertyName != nameof(WorldMapSurfaceItem.LogicalName) && e.PropertyName != nameof(WorldMapSurfaceItem.ParentName)))
            return;
        if (!coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document))
            return;
        string previous = e.PropertyName == nameof(WorldMapSurfaceItem.LogicalName)
            ? logicalNameSnapshots.GetValueOrDefault(surface.ImageName, document.Surface.LogicalName)
            : parentNameSnapshots.GetValueOrDefault(surface.ImageName, document.Surface.ParentName ?? string.Empty);
        string current = e.PropertyName == nameof(WorldMapSurfaceItem.LogicalName) ? surface.LogicalName : surface.ParentName;
        if (string.Equals(previous, current, StringComparison.Ordinal))
            return;
        if (transactionService == null)
        {
            RecordPresentationChange(surface, $"Edit {e.PropertyName}");
        }
        else
        {
            ApplyHierarchyTransaction(surface, e.PropertyName == nameof(WorldMapSurfaceItem.LogicalName), current);
        }
        logicalNameSnapshots[surface.ImageName] = surface.LogicalName;
        parentNameSnapshots[surface.ImageName] = surface.ParentName;
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RenderCanvas();
    }

    private void Link_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (suppressDirty || sender is not WorldMapLinkItem link)
            return;
        WorldMapSurfaceItem? surface = viewModel.Surfaces.FirstOrDefault(item => item.Links.Contains(link));
        if (surface == null)
            return;
        RecordPresentationChange(surface, "Edit map link");
        surface.IsDirty = true;
        viewModel.IsDirty = true;
    }

    private void Fog_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (suppressDirty || sender is not WorldMapFogItem fog)
            return;
        WorldMapSurfaceItem? surface = viewModel.Surfaces.FirstOrDefault(item => item.FogLayers.Contains(fog));
        if (surface == null)
            return;
        RecordPresentationChange(surface, "Edit fog layer");
        surface.IsDirty = true;
        viewModel.IsDirty = true;
    }

    private void Marker_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (suppressDirty || sender is not WorldMapMarkerItem marker || viewModel.SelectedSurface == null)
            return;
        if (e.PropertyName is nameof(WorldMapMarkerItem.IsSelected)
            or nameof(WorldMapMarkerItem.CanvasX)
            or nameof(WorldMapMarkerItem.CanvasY)
            or nameof(WorldMapMarkerItem.MarkerImage)
            or nameof(WorldMapMarkerItem.DisplayName))
            return;
        RecordPresentationChange(viewModel.SelectedSurface, $"Edit marker {marker.NativeKey}");
        viewModel.SelectedSurface.IsDirty = true;
        viewModel.IsDirty = true;
        RenderCanvas();
    }

    private void RefreshMapSearch()
    {
        viewModel.MapSearchResults.Clear();
        string query = viewModel.MapSearchText?.Trim() ?? string.Empty;
        if (query.Length == 0 || Program.InfoManager?.MapsNameCache == null)
            return;
        foreach ((string key, Tuple<string, string, string> names) in Program.InfoManager.MapsNameCache
            .Where(pair => int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Where(pair => string.IsNullOrWhiteSpace(query)
                || pair.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (pair.Value?.Item1?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (pair.Value?.Item2?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (pair.Value?.Item3?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(120))
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId))
                continue;
            viewModel.MapSearchResults.Add(new WorldMapMapSearchItem
            {
                MapId = mapId,
                StreetName = names?.Item1 ?? string.Empty,
                MapName = names?.Item2 ?? string.Empty,
                CategoryName = names?.Item3 ?? string.Empty,
            });
        }
    }

    private async Task LoadAvailabilityAsync()
    {
        availabilityScanCancellation.Cancel();
        availabilityScanCancellation.Dispose();
        availabilityScanCancellation = new CancellationTokenSource();
        CancellationToken token = availabilityScanCancellation.Token;
        viewModel.DerivedAvailability.Clear();
        WorldMapAvailabilityIndex? index = availabilityIndex;
        int[] mapIds = viewModel.SelectedMarker?.MapIds.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
        if (index == null || mapIds.Length == 0)
            return;
        try
        {
            await foreach (WorldMapAvailabilityRecord record in index.ScanAsync(mapIds, token))
            {
                token.ThrowIfCancellationRequested();
                string npc = string.Join(", ", record.NpcOccurrences.Select(pair => $"{pair.Key}×{pair.Value}"));
                string mob = string.Join(", ", record.MobOccurrences.Select(pair => $"{pair.Key}×{pair.Value}"));
                viewModel.DerivedAvailability.Add(new WorldMapAvailabilityItem
                {
                    MapId = record.MapId,
                    MapName = record.MapName,
                    StreetName = record.StreetName,
                    NpcSummary = string.IsNullOrWhiteSpace(npc) ? "-" : npc,
                    MobSummary = string.IsNullOrWhiteSpace(mob) ? "-" : mob,
                    DiagnosticSummary = string.Join("; ", record.Diagnostics),
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AddDiagnostic("Warning", "availability", exception.Message);
        }
    }

    private void OpenDerivedMap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not int mapId || mapId <= 0)
            return;
        Program.HaEditorWindow?.ShowMapExplorer(mapId.ToString(CultureInfo.InvariantCulture));
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MapSelectionFound", mapId, 1);
    }

    private void RecordPresentationChange(WorldMapSurfaceItem surface, string description)
    {
        if (surface == null || !coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document))
            return;
        if (!editSessions.TryGetValue(surface.ImageName, out WorldMapEditSession? session))
        {
            session = new WorldMapEditSession(document);
            editSessions[surface.ImageName] = session;
        }
        session.Record(description, candidate => WorldMapWorkspaceSource.ApplyPresentationToDocument(surface, candidate));
        document.IsDirty = true;
    }

    private WorldMapTransactionService? CreateTransactionService(IEnumerable<WorldMapDocument>? documents = null)
    {
        if (Program.DataSource == null)
            return null;
        return new WorldMapTransactionService(
            new WorldMapSourceOperations(Program.DataSource),
            documents ?? coreDocuments.Values,
            markerRegistry);
    }

    private void ApplyHierarchyTransaction(WorldMapSurfaceItem surface, bool rename, string value)
    {
        if (!coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? selected))
            return;
        WorldMapTransactionService? service = CreateTransactionService();
        if (service == null)
        {
            RecordPresentationChange(surface, rename ? "Rename surface" : "Reparent surface");
            return;
        }

        Dictionary<string, WorldMapDocument> working = coreDocuments.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.DeepClone(),
            StringComparer.OrdinalIgnoreCase);
        if (!working.TryGetValue(surface.ImageName, out WorldMapDocument? workingSelected))
            return;
        WorldMapTransactionService workingService = CreateTransactionService(working.Values)!;
        if (rename)
            workingService.RenameLogical(workingSelected, value, working.Values);
        else
            workingService.Reparent(workingSelected, value, working.Values);
        ApplyDocumentSnapshots(working, rename ? "Rename surface and rewrite references" : "Reparent surface and rewrite references");
        transactionService = CreateTransactionService();
    }

    private void ApplyDocumentSnapshots(IReadOnlyDictionary<string, WorldMapDocument> snapshots, string description)
    {
        foreach ((string key, WorldMapDocument after) in snapshots)
        {
            if (!coreDocuments.TryGetValue(key, out WorldMapDocument? current) ||
                WorldMapSemanticComparer.Compare(current, after).IsEquivalent)
                continue;
            if (!editSessions.TryGetValue(key, out WorldMapEditSession? session))
                editSessions[key] = session = new WorldMapEditSession(current);
            WorldMapDocument copy = after.DeepClone();
            session.Record(description, candidate => candidate.ReplaceFrom(copy));
            if (viewModel.Surfaces.FirstOrDefault(item => string.Equals(item.ImageName, key, StringComparison.OrdinalIgnoreCase)) is WorldMapSurfaceItem item)
            {
                suppressDirty = true;
                WorldMapWorkspaceSource.ApplyCoreDocument(item, session.Document);
                suppressDirty = false;
                item.IsDirty = true;
                logicalNameSnapshots[key] = item.LogicalName;
                parentNameSnapshots[key] = item.ParentName;
                AttachSurfaceHandlers(item);
            }
        }
        viewModel.IsDirty = true;
        RenderCanvas();
    }

    private void Surface_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (surfaceList.SelectedItem is WorldMapSurfaceItem surface)
            viewModel.SelectedSurface = surface;
    }

    private void Marker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WorldMapMarkerItem marker)
        {
            viewModel.SelectedMarker = marker;
            e.Handled = true;
        }
    }

    private void Marker_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (placingMarker || sender is not FrameworkElement element || element.DataContext is not WorldMapMarkerItem marker)
            return;
        viewModel.SelectedMarker = marker;
        draggedMarker = marker;
        markerDragElement = element;
        markerDragStartPoint = e.GetPosition(worldCanvas);
        markerDragStartX = marker.X;
        markerDragStartY = marker.Y;
        markerDragMoved = false;
        element.Focus();
        Mouse.Capture(element);
    }

    private void Marker_MouseMove(object sender, MouseEventArgs e)
    {
        if (draggedMarker == null || markerDragElement == null || e.LeftButton != MouseButtonState.Pressed)
            return;
        Point point = e.GetPosition(worldCanvas);
        Vector delta = point - markerDragStartPoint;
        if (!markerDragMoved && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        markerDragMoved = true;
        MoveMarkerTo(draggedMarker, markerDragStartX + (int)Math.Round(delta.X), markerDragStartY + (int)Math.Round(delta.Y));
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MarkerMoving", draggedMarker.X, draggedMarker.Y);
        e.Handled = true;
    }

    private void Marker_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (draggedMarker == null)
            return;
        bool moved = markerDragMoved;
        CompleteMarkerDrag();
        e.Handled = moved;
    }

    private void CompleteMarkerDrag()
    {
        WorldMapMarkerItem? marker = draggedMarker;
        bool moved = markerDragMoved && marker != null && (marker.X != markerDragStartX || marker.Y != markerDragStartY);
        ReleaseMarkerDragCapture();
        if (!moved || marker == null || viewModel.SelectedSurface == null)
            return;
        viewModel.SelectedSurface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(viewModel.SelectedSurface, $"Move marker {marker.NativeKey}");
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MarkerMoved", marker.X, marker.Y);
    }

    private void CancelMarkerDrag(bool revert, bool updateStatus = true)
    {
        if (draggedMarker == null)
            return;
        if (revert)
            MoveMarkerTo(draggedMarker, markerDragStartX, markerDragStartY);
        ReleaseMarkerDragCapture();
        if (updateStatus)
            viewModel.StatusText = WorldMapEditorTextExtension.Get("MarkerMoveCancelled");
    }

    private void ReleaseMarkerDragCapture()
    {
        if (markerDragElement?.IsMouseCaptured == true)
            markerDragElement.ReleaseMouseCapture();
        draggedMarker = null;
        markerDragElement = null;
        markerDragMoved = false;
    }

    private void MoveMarkerTo(WorldMapMarkerItem marker, int x, int y)
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null)
            return;
        int minX = -surface.BaseOriginX;
        int minY = -surface.BaseOriginY;
        int maxX = surface.BaseWidth - surface.BaseOriginX;
        int maxY = surface.BaseHeight - surface.BaseOriginY;
        suppressDirty = true;
        try
        {
            marker.X = Math.Clamp(x, minX, maxX);
            marker.Y = Math.Clamp(y, minY, maxY);
        }
        finally
        {
            suppressDirty = false;
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        bool clickedMarker = e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(canvasItemsControl, source) != null;
        if (!placingMarker)
        {
            if (!clickedMarker)
            {
                viewModel.SelectedMarker = null;
                worldCanvas.Focus();
            }
            return;
        }
        if (viewModel.SelectedSurface == null)
            return;
        if (clickedMarker)
            return;
        Point point = e.GetPosition(worldCanvas);
        int originX = viewModel.SelectedSurface.BaseOriginX;
        int originY = viewModel.SelectedSurface.BaseOriginY;
        int x = (int)Math.Round(point.X) - originX;
        int y = (int)Math.Round(point.Y) - originY;
        AddMarkerAt(x, y);
        CancelMarkerPlacement(updateStatus: false);
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MarkerPlaced", x, y);
        e.Handled = true;
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!placingMarker)
            return;
        CancelMarkerPlacement();
        e.Handled = true;
    }

    private void Workspace_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && draggedMarker != null)
        {
            CancelMarkerDrag(revert: true);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && placingMarker)
        {
            CancelMarkerPlacement();
            e.Handled = true;
            return;
        }
        if (IsTextEntryFocused())
            return;
        if (e.Key == Key.Delete)
        {
            if (HasKeyboardFocus(referencedMapsListBox) && referencedMapsListBox.SelectedItem is int mapId)
                RemoveMapReference(mapId);
            else if (HasKeyboardFocus(linksListBox))
                RemoveLink();
            else if (HasKeyboardFocus(fogListBox))
                RemoveFog();
            else if (HasKeyboardFocus(worldCanvas) || HasKeyboardFocus(canvasItemsControl))
                RemoveMarker();
            else
                return;
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            && viewModel.SelectedMarker != null
            && (HasKeyboardFocus(worldCanvas) || HasKeyboardFocus(canvasItemsControl)))
        {
            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            int dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
            int dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
            NudgeSelectedMarker(dx, dy);
            e.Handled = true;
        }
    }

    private static bool IsTextEntryFocused() => Keyboard.FocusedElement is TextBoxBase or PasswordBox
        || Keyboard.FocusedElement is ComboBox { IsEditable: true };

    private static bool HasKeyboardFocus(UIElement? element) => element?.IsKeyboardFocusWithin == true;

    private void NudgeSelectedMarker(int dx, int dy)
    {
        WorldMapMarkerItem? marker = viewModel.SelectedMarker;
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (marker == null || surface == null)
            return;
        int oldX = marker.X;
        int oldY = marker.Y;
        MoveMarkerTo(marker, marker.X + dx, marker.Y + dy);
        if (marker.X == oldX && marker.Y == oldY)
            return;
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, $"Nudge marker {marker.NativeKey}");
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MarkerMoved", marker.X, marker.Y);
    }

    private void Canvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;
        ZoomBy(e.Delta > 0 ? 0.1 : -0.1);
        e.Handled = true;
    }

    private void NewSurface()
    {
        string baseName = "WorldMapNew";
        int suffix = 1;
        while (viewModel.Surfaces.Any(surface => string.Equals(surface.ImageName, baseName, StringComparison.OrdinalIgnoreCase)))
            baseName = $"WorldMapNew{suffix++}";
        var surface = new WorldMapSurfaceItem { ImageName = baseName, LogicalName = baseName, IsDirty = true };
        AddMarker(surface, 0, 0, 0, Array.Empty<int>());
        WorldMapDocument document = WorldMapDocument.CreateNew(baseName, baseName);
        document.Surface.AddEntry("0").Spot = new System.Drawing.Point(0, 0);
        coreDocuments[baseName] = document;
        editSessions[baseName] = new WorldMapEditSession(document);
        AttachSurfaceHandlers(surface);
        viewModel.Surfaces.Add(surface);
        viewModel.SelectedSurface = surface;
        viewModel.IsDirty = true;
        transactionService = CreateTransactionService();
        AddDiagnostic("Info", surface.ImagePath, WorldMapEditorTextExtension.Get("Ready"));
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void DuplicateSurface()
    {
        WorldMapSurfaceItem? source = viewModel.SelectedSurface;
        if (source == null)
            return;
        string baseName = source.ImageName + "_copy";
        string name = baseName;
        int suffix = 2;
        while (viewModel.Surfaces.Any(surface => string.Equals(surface.ImageName, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{baseName}{suffix++}";
        var copy = new WorldMapSurfaceItem { ImageName = name, LogicalName = name, ParentName = source.ParentName, BaseWidth = source.BaseWidth, BaseHeight = source.BaseHeight, BaseOriginX = source.BaseOriginX, BaseOriginY = source.BaseOriginY, IsDirty = true, LinkCount = source.LinkCount, FogCount = source.FogCount };
        if (coreDocuments.TryGetValue(source.ImageName, out WorldMapDocument? typed))
        {
            WorldMapDocument duplicated = typed.DeepClone();
            duplicated.ImageName = name + ".img";
            duplicated.Surface.LogicalName = name;
        duplicated.MarkNew();
            duplicated.IsDirty = true;
            coreDocuments[name] = duplicated;
            editSessions[name] = new WorldMapEditSession(duplicated);
            WorldMapWorkspaceSource.ApplyCoreDocument(copy, duplicated);
        }
        else
        {
            foreach (WorldMapMarkerItem marker in source.Markers)
                AddMarker(copy, marker.MarkerType, marker.X, marker.Y, marker.MapIds, marker.Title, marker.Description, marker.TownDescription);
        }
        viewModel.Surfaces.Add(copy);
        AttachSurfaceHandlers(copy);
        viewModel.SelectedSurface = copy;
        viewModel.IsDirty = true;
        transactionService = CreateTransactionService();
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void BeginAddMarker()
    {
        if (viewModel.SelectedSurface == null)
            return;
        placingMarker = true;
        if (addMarkerToolButton != null)
            addMarkerToolButton.IsChecked = true;
        worldCanvas.Cursor = Cursors.Cross;
        viewModel.StatusText = WorldMapEditorTextExtension.Get("PlaceMarkerHint");
        worldCanvas.Focus();
    }

    private void AddMarkerTool_Click(object sender, RoutedEventArgs e)
    {
        if (addMarkerToolButton.IsChecked == true)
            BeginAddMarker();
        else
            CancelMarkerPlacement();
    }

    private void DeleteMarkerTool_Click(object sender, RoutedEventArgs e) => RemoveMarker();

    private void CancelMarkerPlacement(bool updateStatus = true)
    {
        if (!placingMarker && addMarkerToolButton?.IsChecked != true)
            return;
        placingMarker = false;
        if (addMarkerToolButton != null)
            addMarkerToolButton.IsChecked = false;
        if (worldCanvas != null)
            worldCanvas.Cursor = null;
        if (updateStatus)
            viewModel.StatusText = WorldMapEditorTextExtension.Get("MarkerPlacementCancelled");
    }

    private void AddMarkerAt(int x, int y)
    {
        if (viewModel.SelectedSurface == null)
            return;
        WorldMapMarkerItem marker = AddMarker(viewModel.SelectedSurface, 0, x, y, Array.Empty<int>());
        viewModel.SelectedMarker = marker;
        viewModel.SelectedSurface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(viewModel.SelectedSurface, "Add marker");
        RenderCanvas();
    }

    private static WorldMapMarkerItem AddMarker(WorldMapSurfaceItem surface, int markerType, int x, int y, IEnumerable<int> mapIds, string? title = null, string? description = null, string? townDescription = null)
    {
        var marker = new WorldMapMarkerItem { NativeKey = surface.Markers.Count.ToString(CultureInfo.InvariantCulture), MarkerType = markerType, X = x, Y = y, Title = title ?? string.Empty, Description = description ?? string.Empty, TownDescription = townDescription ?? string.Empty };
        marker.SetCanvasOrigin(surface.BaseOriginX, surface.BaseOriginY);
        marker.SetMapIds(mapIds);
        surface.Markers.Add(marker);
        return marker;
    }

    private void AddLink()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null)
            return;
        WorldMapLinkItem link = new() { NativeKey = surface.Links.Count.ToString(CultureInfo.InvariantCulture), TargetName = "WorldMapNew" };
        link.PropertyChanged += Link_PropertyChanged;
        surface.Links.Add(link);
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, "Add map link");
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void AddFog()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null)
            return;
        WorldMapFogItem fog = new() { NativeKey = surface.FogLayers.Count.ToString(CultureInfo.InvariantCulture) };
        fog.PropertyChanged += Fog_PropertyChanged;
        surface.FogLayers.Add(fog);
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        viewModel.SelectedFog = surface.FogLayers.LastOrDefault();
        RecordPresentationChange(surface, "Add fog layer");
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void RemoveMarker()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        WorldMapMarkerItem? marker = viewModel.SelectedMarker;
        if (surface == null || marker == null || !surface.Markers.Remove(marker))
            return;
        string markerKey = marker.NativeKey;
        viewModel.SelectedMarker = surface.Markers.FirstOrDefault();
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, "Remove marker");
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MarkerDeleted", markerKey);
        RenderCanvas();
    }

    private void RemoveLink()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        WorldMapLinkItem? link = viewModel.SelectedLink;
        if (surface == null || link == null || !surface.Links.Remove(link))
            return;
        viewModel.SelectedLink = surface.Links.FirstOrDefault();
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, "Remove map link");
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void RemoveFog()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        WorldMapFogItem? fog = viewModel.SelectedFog;
        if (surface == null || fog == null || !surface.FogLayers.Remove(fog))
            return;
        viewModel.SelectedFog = surface.FogLayers.FirstOrDefault();
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, "Remove fog layer");
        viewModel.StatusText = WorldMapEditorTextExtension.Get("Ready");
    }

    private void AddMapToMarker()
    {
        WorldMapMarkerItem? marker = viewModel.SelectedMarker;
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        WorldMapMapSearchItem? map = viewModel.SelectedMapSearchItem;
        if (marker == null || surface == null || map == null)
            return;
        AddMapReference(marker, surface, map.MapId);
    }

    private void MapSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AddMapToMarker();
        e.Handled = true;
    }

    private void MapSearchResults_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        AddMapToMarker();
        e.Handled = true;
    }

    private void AddMapReference_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedMarker == null || viewModel.SelectedSurface == null)
            return;
        var picker = new LoadMapSelector { Owner = this };
        if (picker.ShowDialog() != true || !int.TryParse(picker.SelectedMap, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId))
            return;
        AddMapReference(viewModel.SelectedMarker, viewModel.SelectedSurface, mapId);
    }

    private void RemoveMapReference_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedMarker == null || viewModel.SelectedSurface == null || referencedMapsListBox.SelectedItem is not int mapId)
            return;
        RemoveMapReference(mapId);
    }

    private void RemoveMapReference(int mapId)
    {
        if (viewModel.SelectedMarker == null || viewModel.SelectedSurface == null)
            return;
        bool removed;
        suppressDirty = true;
        try { removed = viewModel.SelectedMarker.RemoveMapId(mapId); }
        finally { suppressDirty = false; }
        if (!removed)
            return;
        viewModel.SelectedSurface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(viewModel.SelectedSurface, "Remove map reference");
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MapRemoved", mapId);
    }

    private void AddMapReference(WorldMapMarkerItem marker, WorldMapSurfaceItem surface, int mapId)
    {
        bool added;
        suppressDirty = true;
        try { added = marker.AddMapId(mapId); }
        finally { suppressDirty = false; }
        if (!added)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Format("MapAlreadyReferenced", mapId);
            return;
        }
        surface.IsDirty = true;
        viewModel.IsDirty = true;
        RecordPresentationChange(surface, "Add map reference");
        viewModel.StatusText = WorldMapEditorTextExtension.Format("MapAdded", mapId);
    }

    private void CreateChild()
    {
        WorldMapSurfaceItem? parentSurface = viewModel.SelectedSurface;
        if (parentSurface == null || !coreDocuments.TryGetValue(parentSurface.ImageName, out WorldMapDocument? parent))
            return;
        string baseName = string.IsNullOrWhiteSpace(parent.Surface.LogicalName) ? parent.ImageName : parent.Surface.LogicalName;
        baseName = baseName.Trim() + "_child";
        string childName = baseName;
        int suffix = 2;
        while (coreDocuments.Keys.Any(name => string.Equals(name, childName, StringComparison.OrdinalIgnoreCase)))
            childName = $"{baseName}{suffix++}";
        WorldMapTransactionService? service = CreateTransactionService();
        if (service == null)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            return;
        }
        Dictionary<string, WorldMapDocument> working = coreDocuments.ToDictionary(pair => pair.Key, pair => pair.Value.DeepClone(), StringComparer.OrdinalIgnoreCase);
        WorldMapDocument workingParent = working[parentSurface.ImageName];
        WorldMapDocument child = service.CreateChild(workingParent, childName, childName);
        working[childName] = child;
        ApplyDocumentSnapshots(working, "Create WorldMap child and reciprocal link");
        coreDocuments[childName] = child;
        editSessions[childName] = new WorldMapEditSession(child);
        var childSurface = new WorldMapSurfaceItem { ImageName = childName, LogicalName = child.Surface.LogicalName, ParentName = child.Surface.ParentName, IsDirty = true };
        WorldMapWorkspaceSource.ApplyCoreDocument(childSurface, child);
        AttachSurfaceHandlers(childSurface);
        viewModel.Surfaces.Add(childSurface);
        viewModel.SelectedSurface = childSurface;
        viewModel.IsDirty = true;
        transactionService = CreateTransactionService();
        viewModel.StatusText = WorldMapEditorTextExtension.Format("TransactionComplete", 2);
    }

    private void RemoveFromHierarchy()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null || !coreDocuments.ContainsKey(surface.ImageName))
            return;
        WorldMapTransactionService? service = CreateTransactionService();
        if (service == null)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            return;
        }
        Dictionary<string, WorldMapDocument> working = coreDocuments.ToDictionary(pair => pair.Key, pair => pair.Value.DeepClone(), StringComparer.OrdinalIgnoreCase);
        service = CreateTransactionService(working.Values);
        int changed = service?.RemoveFromHierarchy(working[surface.ImageName], working.Values) ?? 0;
        ApplyDocumentSnapshots(working, "Remove surface from hierarchy and rewrite references");
        transactionService = CreateTransactionService();
        viewModel.StatusText = WorldMapEditorTextExtension.Format("TransactionComplete", Math.Max(1, changed));
    }

    private void DeleteAsset()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null || !coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document))
            return;
        if (MessageBox.Show(this, WorldMapEditorTextExtension.Get("DeleteConfirm"), WorldMapEditorTextExtension.Get("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        WorldMapTransactionService? service = CreateTransactionService();
        if (service == null)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            return;
        }
        WorldMapTransactionResult result = service.Delete(document, coreDocuments.Values);
        if (!result.Succeeded)
        {
            string error = result.Errors.FirstOrDefault() ?? WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            viewModel.StatusText = WorldMapEditorTextExtension.Format("DeleteBlocked", error);
            AddDiagnostic("Error", surface.ImagePath, error);
            return;
        }
        viewModel.Surfaces.Remove(surface);
        coreDocuments.Remove(surface.ImageName);
        editSessions.Remove(surface.ImageName);
        logicalNameSnapshots.Remove(surface.ImageName);
        parentNameSnapshots.Remove(surface.ImageName);
        viewModel.SelectedSurface = viewModel.Surfaces.FirstOrDefault();
        viewModel.IsDirty = viewModel.Surfaces.Any(item => item.IsDirty);
        transactionService = CreateTransactionService();
        string paths = string.Join(", ", result.AffectedImages);
        viewModel.StatusText = WorldMapEditorTextExtension.Format("TransactionComplete", result.AffectedImages.Count);
        AddDiagnostic("Info", paths, WorldMapEditorTextExtension.Get("Ready"));
    }

    private void ReplaceBackground()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null || !coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document) || document.Surface.BaseImage == null)
        {
            string unavailable = WorldMapEditorTextExtension.Get("PreviewUnavailable");
            AddDiagnostic("Warning", surface?.ImagePath ?? string.Empty, unavailable);
            viewModel.StatusText = unavailable;
            return;
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = WorldMapEditorTextExtension.Get("ReplaceBackground"),
            Filter = "Image files|*.png;*.bmp;*.jpg;*.jpeg|All files|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            if (!editSessions.TryGetValue(surface.ImageName, out WorldMapEditSession? session))
                editSessions[surface.ImageName] = session = new WorldMapEditSession(document);
            WorldMapCanvasRef imported = WorldMapCanvasService.Import(dialog.FileName, document.Surface.BaseImage.Origin, document.Surface.BaseImage.Z);
            using System.Drawing.Bitmap replacement = imported.RawProperty?.GetBitmap()
                ?? throw new InvalidOperationException(WorldMapEditorTextExtension.Get("PreviewUnavailable"));
            session.Record("Replace BaseImg background", candidate =>
            {
                if (candidate.Surface.BaseImage == null)
                    candidate.Surface.BaseImage = imported.DeepClone();
                else
                    WorldMapCanvasService.ReplaceBitmap(candidate.Surface.BaseImage, replacement);
            });
            suppressDirty = true;
            WorldMapWorkspaceSource.ApplyCoreDocument(surface, session.Document);
            suppressDirty = false;
            surface.IsDirty = true;
            viewModel.IsDirty = true;
            previewCache?.InvalidateSource(viewModel.SourceName);
            RenderCanvas();
            viewModel.StatusText = WorldMapEditorTextExtension.Get("BackgroundReplaced");
        }
        catch (Exception exception)
        {
            AddDiagnostic("Error", surface.ImagePath, exception.Message);
            viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveFailed", exception.Message);
        }
    }

    private void SaveSelected()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null)
            return;
        if (coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document) && transactionService != null)
        {
            WorldMapValidationResult validation = WorldMapValidator.ValidateAll(coreDocuments.Values, markerRegistry);
            PublishValidationDiagnostics(validation);
            if (validation.Errors.Count > 0)
            {
                viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationFailed", validation.Errors[0].Message);
                return;
            }
            WorldMapTransactionResult result = transactionService.Commit(new[] { document });
            if (!result.Succeeded)
            {
                string error = result.Errors.FirstOrDefault() ?? WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
                viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveFailed", error);
                AddDiagnostic("Error", string.Join(", ", result.AffectedImages), error);
                return;
            }
            MarkDocumentsSaved(new[] { document });
            viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveComplete", result.AffectedImages.Count);
            AddDiagnostic("Info", string.Join(", ", result.AffectedImages), WorldMapEditorTextExtension.Get("Ready"));
            return;
        }
        if (!WorldMapWorkspaceSource.TryWrite(surface, coreDocuments.GetValueOrDefault(surface.ImageName)))
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            AddDiagnostic("Warning", surface.ImagePath, WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder"));
            return;
        }
        surface.IsDirty = false;
        viewModel.IsDirty = viewModel.Surfaces.Any(item => item.IsDirty);
        viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveComplete", 1);
    }

    private void SaveAll()
    {
        WorldMapDocument[] candidates = coreDocuments.Values.Where(document => document.IsDirty || document.IsNew).ToArray();
        if (transactionService != null && candidates.Length > 0)
        {
            WorldMapValidationResult validation = WorldMapValidator.ValidateAll(coreDocuments.Values, markerRegistry);
            PublishValidationDiagnostics(validation);
            if (validation.Errors.Count > 0)
            {
                viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationFailed", validation.Errors[0].Message);
                return;
            }
            WorldMapTransactionResult result = transactionService.Commit(candidates);
            if (!result.Succeeded)
            {
                string error = result.Errors.FirstOrDefault() ?? WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
                viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveFailed", error);
                AddDiagnostic("Error", string.Join(", ", result.AffectedImages), error);
                return;
            }
            MarkDocumentsSaved(candidates);
            viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveComplete", result.AffectedImages.Count);
            AddDiagnostic("Info", string.Join(", ", result.AffectedImages), WorldMapEditorTextExtension.Get("Ready"));
            return;
        }
        int saved = 0;
        foreach (WorldMapSurfaceItem surface in viewModel.Surfaces.Where(item => item.IsDirty).ToArray())
            if (WorldMapWorkspaceSource.TryWrite(surface, coreDocuments.GetValueOrDefault(surface.ImageName)))
            {
                surface.IsDirty = false;
                saved++;
            }
        if (saved == 0 && viewModel.IsDirty)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder");
            AddDiagnostic("Warning", "Map/WorldMap", WorldMapEditorTextExtension.Get("ReadOnlyPlaceholder"));
            return;
        }
        viewModel.IsDirty = viewModel.Surfaces.Any(item => item.IsDirty);
        viewModel.StatusText = WorldMapEditorTextExtension.Format("SaveComplete", saved);
    }

    private void MarkDocumentsSaved(IEnumerable<WorldMapDocument> documents)
    {
        foreach (WorldMapDocument document in documents ?? Enumerable.Empty<WorldMapDocument>())
        {
            if (editSessions.TryGetValue(document.ImageName, out WorldMapEditSession? session))
                session.MarkSaved();
            if (viewModel.Surfaces.FirstOrDefault(item => string.Equals(item.ImageName, document.ImageName, StringComparison.OrdinalIgnoreCase)) is WorldMapSurfaceItem surface)
            {
                suppressDirty = true;
                WorldMapWorkspaceSource.ApplyCoreDocument(surface, document);
                suppressDirty = false;
                surface.IsDirty = false;
                logicalNameSnapshots[document.ImageName] = surface.LogicalName;
                parentNameSnapshots[document.ImageName] = surface.ParentName;
                AttachSurfaceHandlers(surface);
            }
        }
        viewModel.IsDirty = viewModel.Surfaces.Any(item => item.IsDirty);
    }

    private void ValidateSelected()
    {
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface == null)
            return;
        if (coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document))
        {
            WorldMapValidationResult result = WorldMapValidator.Validate(document, new WorldMapHierarchyIndex(coreDocuments.Values), markerRegistry, WorldMapWorkspaceSource.BuildValidationContext());
            PublishValidationDiagnostics(result, document.ImageName);
            viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationComplete", result.Diagnostics.Count);
            return;
        }
        int count = surface.Diagnostics.Count;
        viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationComplete", count);
    }

    private void ValidateAll()
    {
        WorldMapValidationResult result = WorldMapValidator.ValidateAll(coreDocuments.Values, markerRegistry, WorldMapWorkspaceSource.BuildValidationContext());
        PublishValidationDiagnostics(result);
        foreach (WorldMapSurfaceItem surface in viewModel.Surfaces.Where(item => !coreDocuments.ContainsKey(item.ImageName)))
            AddTypedDiagnostic(surface.ImageName, new WorldMapDiagnostic(WorldMapDiagnosticSeverity.Error, surface.ImagePath, "WorldMap image could not be loaded and was not checked."));
        int errors = viewModel.Diagnostics.Count(item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        int warnings = viewModel.Diagnostics.Count(item => string.Equals(item.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        viewModel.StatusText = WorldMapEditorTextExtension.Format("ValidationAllComplete", viewModel.Surfaces.Count, errors, warnings);
        CommandManager.InvalidateRequerySuggested();
    }

    private void PublishValidationDiagnostics(WorldMapValidationResult result, string? onlyImageName = null)
    {
        foreach (WorldMapSurfaceItem surface in viewModel.Surfaces)
        {
            surface.Diagnostics.Clear();
            surface.DiagnosticsCount = 0;
        }
        viewModel.Diagnostics.Clear();
        if (result == null)
            return;
        if (!string.IsNullOrWhiteSpace(onlyImageName) && coreDocuments.TryGetValue(onlyImageName, out WorldMapDocument? selectedDocument))
        {
            WorldMapValidationResult filtered = WorldMapValidator.Validate(selectedDocument, new WorldMapHierarchyIndex(coreDocuments.Values), markerRegistry, WorldMapWorkspaceSource.BuildValidationContext());
            foreach (WorldMapDiagnostic diagnostic in filtered.Diagnostics)
                AddTypedDiagnostic(selectedDocument.ImageName, diagnostic);
            return;
        }
        foreach (WorldMapDocument currentDocument in coreDocuments.Values)
        {
            WorldMapValidationResult current = WorldMapValidator.Validate(currentDocument, new WorldMapHierarchyIndex(coreDocuments.Values), markerRegistry, WorldMapWorkspaceSource.BuildValidationContext());
            foreach (WorldMapDiagnostic diagnostic in current.Diagnostics)
                AddTypedDiagnostic(currentDocument.ImageName, diagnostic);
        }
    }

    private void AddTypedDiagnostic(string imageName, WorldMapDiagnostic diagnostic)
    {
        string severity = diagnostic.Severity.ToString();
        string path = string.IsNullOrWhiteSpace(imageName) ? diagnostic.Path : $"Map/WorldMap/{imageName}/{diagnostic.Path}";
        var item = new WorldMapDiagnosticItem { Severity = severity, Path = path, Message = diagnostic.Message };
        viewModel.Diagnostics.Add(item);
        if (viewModel.Surfaces.FirstOrDefault(surface => string.Equals(surface.ImageName, imageName, StringComparison.OrdinalIgnoreCase)) is WorldMapSurfaceItem surface)
        {
            surface.Diagnostics.Add(item);
            surface.DiagnosticsCount = surface.Diagnostics.Count;
        }
    }

    private void ReviewChanges()
    {
        viewModel.ReviewChanges.Clear();
        foreach (WorldMapDocument document in coreDocuments.Values)
        {
            WorldMapDocument baseline;
            try
            {
                baseline = document.RawImage == null
                    ? WorldMapDocument.CreateNew(document.ImageName, document.Surface.LogicalName)
                    : WorldMapCodec.Read(document.RawImage);
            }
            catch
            {
                baseline = WorldMapDocument.CreateNew(document.ImageName, document.Surface.LogicalName);
            }
            WorldMapSemanticReport report = WorldMapSemanticComparer.Compare(baseline, document);
            foreach (WorldMapSemanticChange change in report.Changes)
                viewModel.ReviewChanges.Add(new WorldMapReviewChangeItem
                {
                    ImagePath = $"Map/WorldMap/{document.ImageName}",
                    Path = change.Path,
                    Before = change.Before,
                    After = change.After,
                });
        }
        if (viewModel.ReviewChanges.Count == 0)
        {
            viewModel.StatusText = WorldMapEditorTextExtension.Get("ReviewNoChanges");
            return;
        }
        string[] affectedPaths = viewModel.ReviewChanges.Select(item => item.ImagePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        viewModel.StatusText = WorldMapEditorTextExtension.Format("ReviewSummary", viewModel.ReviewChanges.Count, affectedPaths.Length);
        var dialog = new Window
        {
            Owner = this,
            Title = WorldMapEditorTextExtension.Get("ReviewTitle"),
            Width = 980,
            Height = 620,
            MinWidth = 640,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new DockPanel
                {
                    LastChildFill = true,
                    Children =
                    {
                        new Button
                        {
                            Content = WorldMapEditorTextExtension.Get("Close"),
                            Padding = new Thickness(18, 5, 18, 5),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 0, 0, 10),
                        },
                        new ListBox
                        {
                            ItemsSource = viewModel.ReviewChanges,
                            DisplayMemberPath = nameof(WorldMapReviewChangeItem.DisplayText),
                            BorderThickness = new Thickness(0),
                        },
                    },
                },
            },
        };
        if (dialog.Content is Border border && border.Child is DockPanel panel && panel.Children[0] is Button close)
            close.Click += (_, _) => dialog.Close();
        dialog.ShowDialog();
    }

    private void FitCanvas()
    {
        autoFitCanvas = true;
        ApplyFitCanvasZoom();
    }

    private void ApplyFitCanvasZoom()
    {
        if (canvasScrollViewer == null || viewModel.SelectedSurface == null)
            return;
        double viewportWidth = canvasScrollViewer.ViewportWidth > 0 ? canvasScrollViewer.ViewportWidth : canvasScrollViewer.ActualWidth;
        double viewportHeight = canvasScrollViewer.ViewportHeight > 0 ? canvasScrollViewer.ViewportHeight : canvasScrollViewer.ActualHeight;
        double horizontal = (viewportWidth - 32) / Math.Max(1, viewModel.SelectedSurface.BaseWidth);
        double vertical = (viewportHeight - 32) / Math.Max(1, viewModel.SelectedSurface.BaseHeight);
        viewModel.Zoom = Math.Clamp(Math.Min(horizontal, vertical), 0.2, 2.5);
    }

    private void ZoomBy(double delta)
    {
        autoFitCanvas = false;
        viewModel.Zoom += delta;
    }

    private void RuntimePreview()
    {
        HaCreator.GUI.HaEditor? editor = Owner as HaCreator.GUI.HaEditor ?? Program.HaEditorWindow;
        if (editor != null && HaCreator.GUI.HaEditor.MapSim.CanExecute(null, editor))
        {
            HaCreator.GUI.HaEditor.MapSim.Execute(null, editor);
            viewModel.StatusText = WorldMapEditorTextExtension.Get("RuntimePreviewStarted");
            return;
        }
        string unavailable = WorldMapEditorTextExtension.Get("RuntimePreviewUnavailable");
        AddDiagnostic("Warning", viewModel.SelectedSurface?.ImagePath ?? string.Empty, unavailable);
        viewModel.StatusText = unavailable;
    }

    private void AddDiagnostic(string severity, string path, string message)
    {
        var diagnostic = new WorldMapDiagnosticItem { Severity = severity, Path = path, Message = message };
        viewModel.Diagnostics.Add(diagnostic);
        viewModel.SelectedSurface?.Diagnostics.Add(diagnostic);
        if (viewModel.SelectedSurface != null)
            viewModel.SelectedSurface.DiagnosticsCount = viewModel.SelectedSurface.Diagnostics.Count;
    }

    private void RenderCanvas()
    {
        if (canvasItemsControl == null)
            return;
        WorldMapSurfaceItem? surface = viewModel.SelectedSurface;
        if (surface != null)
        {
            foreach (WorldMapMarkerItem marker in surface.Markers)
                marker.SetCanvasOrigin(surface.BaseOriginX, surface.BaseOriginY);
            if (baseImage != null && coreDocuments.TryGetValue(surface.ImageName, out WorldMapDocument? document))
            {
                WzCanvasProperty? canvas = document.Surface.BaseImage?.RawProperty;
                baseImage.Source = previewCache?.GetOrCreate(
                    viewModel.SourceName,
                    $"{surface.ImagePath}/BaseImg/0",
                    canvas);
                if (baseImage.Source == null && canvas != null)
                    viewModel.StatusText = WorldMapEditorTextExtension.Get("PreviewUnavailable");
            }
            ResolveMarkerPreviews(surface);
        }
        canvasItemsControl.Items.Refresh();
    }

    private void ResolveMarkerPreviews(WorldMapSurfaceItem surface)
    {
        if (previewCache == null)
            return;
        WzImage? helper = null;
        try { helper = Program.FindImage("Map", "MapHelper.img"); } catch { }
        if (helper == null)
            return;
        foreach (WorldMapMarkerItem marker in surface.Markers)
        {
            if (!markerRegistry.Contains(marker.MarkerType))
            {
                marker.MarkerImage = null;
                continue;
            }
            try
            {
                WzCanvasProperty? canvas = helper.GetFromPath($"worldMap/mapImage/{marker.MarkerType}") as WzCanvasProperty;
                marker.MarkerImage = previewCache.GetOrCreate(viewModel.SourceName, $"Map/MapHelper.img/worldMap/mapImage/{marker.MarkerType}", canvas);
            }
            catch
            {
                marker.MarkerImage = null;
            }
        }
    }

    private void SubscribeToHotSwap()
    {
        HotSwapRefreshService? service = Program.HaEditorWindow?.hcsm?.HotSwapService;
        if (ReferenceEquals(service, hotSwapService))
            return;
        if (hotSwapService != null)
            hotSwapService.WorldMapDataChanged -= HotSwapService_WorldMapDataChanged;
        hotSwapService = service;
        if (hotSwapService != null)
            hotSwapService.WorldMapDataChanged += HotSwapService_WorldMapDataChanged;
    }

    private async void HotSwapService_WorldMapDataChanged(object? sender, WorldMapDataChangedEventArgs e)
    {
        if (!catalogLoaded || availabilityCancellation.IsCancellationRequested)
            return;
        string path = string.IsNullOrWhiteSpace(e.RelativePath) ? e.ImageName : e.RelativePath;
        if (viewModel.IsDirty)
        {
            AddDiagnostic("Warning", path, WorldMapEditorTextExtension.Format("ExternalConflict", path));
            viewModel.StatusText = WorldMapEditorTextExtension.Format("ExternalConflict", path);
            previewCache?.InvalidateSource(viewModel.SourceName);
            availabilityIndex?.InvalidateAll();
            return;
        }
        previewCache?.InvalidateSource(viewModel.SourceName);
        availabilityIndex?.InvalidateAll();
        await LoadCatalogAsync();
    }

    private void Workspace_Closing(object? sender, CancelEventArgs e)
    {
        if (viewModel.IsDirty)
        {
            MessageBoxResult result = MessageBox.Show(this, WorldMapEditorTextExtension.Get("UnsavedPrompt"), WorldMapEditorTextExtension.Get("UnsavedTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == MessageBoxResult.Yes)
            {
                SaveAll();
                e.Cancel = viewModel.IsDirty;
            }
        }
        if (e.Cancel)
            return;
        hotSwapService?.WorldMapDataChanged -= HotSwapService_WorldMapDataChanged;
        hotSwapService = null;
        availabilityCancellation.Cancel();
        availabilityScanCancellation.Cancel();
        availabilityIndex?.Dispose();
        availabilityIndex = null;
        previewCache?.Dispose();
        previewCache = null;
        availabilityCancellation.Dispose();
        availabilityScanCancellation.Dispose();
    }

    private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (autoFitCanvas && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyFitCanvasZoom);
    }
}

internal sealed class WorldMapCatalogSnapshot
{
    public string SourceName { get; init; } = string.Empty;
    public string SourceMode { get; init; } = string.Empty;
    public List<WorldMapSurfaceItem> Surfaces { get; } = new();
    public Dictionary<string, WorldMapDocument> CoreDocuments { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Source bridge. Core document services are discovered lazily so old builds remain openable.</summary>
internal static class WorldMapWorkspaceSource
{
    public static WorldMapValidationContext BuildValidationContext()
    {
        var ids = new HashSet<int>();
        foreach (string value in Program.InfoManager?.MapsNameCache?.Keys ?? Enumerable.Empty<string>())
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId) && mapId > 0)
                ids.Add(mapId);
        return new WorldMapValidationContext { ExistingMapIds = ids, HasMapInventory = ids.Count > 0 };
    }

    public static WorldMapMarkerRegistry ReadMarkerRegistry()
    {
        try
        {
            WzImage? helper = Program.FindImage("Map", "MapHelper.img");
            return helper == null ? new WorldMapMarkerRegistry() : WorldMapMarkerRegistry.FromImage(helper);
        }
        catch
        {
            return new WorldMapMarkerRegistry();
        }
    }

    public static WorldMapCatalogSnapshot ReadCatalog()
    {
        var snapshot = new WorldMapCatalogSnapshot
        {
            SourceName = Program.DataSource?.VersionInfo?.DisplayName ?? Program.DataSource?.Name ?? WorldMapEditorTextExtension.Get("NoDataSource"),
            SourceMode = Program.DataSource?.GetType().Name.Replace("DataSource", string.Empty, StringComparison.OrdinalIgnoreCase) ?? "Unavailable",
        };
        IEnumerable<string> names = Program.DataSource?.GetImageNamesInDirectory("Map", "WorldMap") ?? Array.Empty<string>();
        foreach (string rawName in names)
        {
            string name = Path.GetFileNameWithoutExtension(rawName.Trim());
            if (string.IsNullOrWhiteSpace(name) || snapshot.Surfaces.Any(surface => string.Equals(surface.ImageName, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            var surface = new WorldMapSurfaceItem { ImageName = name, LogicalName = name };
            object? document = TryReadCoreDocument(name);
            if (document is WorldMapDocument typedDocument)
            {
                snapshot.CoreDocuments[name] = typedDocument;
                ApplyCoreDocument(surface, typedDocument);
            }
            else
            {
                surface.Diagnostics.Add(new WorldMapDiagnosticItem { Severity = "Info", Path = surface.ImagePath, Message = "Core WorldMap codec is unavailable; showing a lazy catalog row." });
            }
            surface.DiagnosticsCount = surface.Diagnostics.Count;
            snapshot.Surfaces.Add(surface);
        }
        return snapshot;
    }

    public static bool TryWrite(WorldMapSurfaceItem surface, object? coreDocument)
    {
        if (coreDocument == null)
            return false;
        if (coreDocument is WorldMapDocument typedDocument && Program.DataSource != null)
        {
            ApplyPresentationToDocument(surface, typedDocument);
            WorldMapBatchSaveResult result = new WorldMapRepository(Program.DataSource).Save(typedDocument);
            return result.Succeeded;
        }
        // The core service owns cloning and source transactions. Invoke a
        // compatible public Write/Save method when present; otherwise leave
        // the document staged and let the status panel explain the gap.
        Type type = coreDocument.GetType();
        MethodInfo? method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate => (candidate.Name is "Write" or "Save" or "Apply") && candidate.GetParameters().Length <= 1);
        if (method == null)
            return false;
        try
        {
            object? argument = method.GetParameters().Length == 0 ? null : Program.FindImage("Map", $"WorldMap/{surface.ImageName}.img");
            method.Invoke(coreDocument, method.GetParameters().Length == 0 ? Array.Empty<object>() : new[] { argument });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? TryReadCoreDocument(string imageName)
    {
        WzImage? image = Program.FindImage("Map", $"WorldMap/{imageName}.img")
            ?? Program.FindImage("Map", $"{imageName}.img")
            ?? Program.DataSource?.GetImageByPath($"Map/WorldMap/{imageName}.img");
        if (image == null)
            return null;
        Type? codecType = typeof(WorldMapCodec);
        if (codecType == null)
            return null;
        foreach (MethodInfo method in codecType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(method => method.Name is "Read" or "Load"))
        {
            try
            {
                object? target = method.IsStatic ? null : Activator.CreateInstance(codecType);
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters.Length > 3)
                    continue;
                object?[] args = new object?[parameters.Length];
                bool assignedImage = false;
                for (int index = 0; index < parameters.Length; index++)
                {
                    Type parameterType = parameters[index].ParameterType;
                    if (parameterType.IsAssignableFrom(image.GetType()) || parameterType == typeof(WzImage))
                    {
                        args[index] = image;
                        assignedImage = true;
                    }
                    else if (parameterType == typeof(string))
                        args[index] = imageName;
                    else if (parameterType == typeof(CancellationToken))
                        args[index] = CancellationToken.None;
                    else if (parameters[index].HasDefaultValue)
                        args[index] = parameters[index].DefaultValue;
                    else
                        args[index] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                }
                if (!assignedImage)
                    continue;
                object? result = method.Invoke(target, args);
                if (result != null)
                    return result;
            }
            catch
            {
                // An old core build may expose a different overload. Try the
                // next compatible method before falling back to metadata rows.
            }
        }
        return null;
    }

    internal static void ApplyCoreDocument(WorldMapSurfaceItem surface, object document)
    {
        surface.Markers.Clear();
        surface.Links.Clear();
        surface.FogLayers.Clear();
        surface.Diagnostics.Clear();
        object root = GetObject(document, "Surface") ?? document;
        surface.LogicalName = GetString(root, "LogicalName", "WorldMap", "Name") ?? surface.LogicalName;
        surface.ParentName = GetString(root, "ParentLogicalName", "ParentName", "ParentMap") ?? string.Empty;
        object? baseCanvas = GetObject(root, "BaseCanvas", "BaseImage", "Canvas");
        surface.BaseWidth = GetInt(baseCanvas, "Width", "PixelWidth") ?? GetInt(document, "BaseWidth") ?? surface.BaseWidth;
        surface.BaseHeight = GetInt(baseCanvas, "Height", "PixelHeight") ?? GetInt(document, "BaseHeight") ?? surface.BaseHeight;
        if (document is WorldMapDocument typed && typed.Surface.BaseImage != null)
        {
            surface.BaseOriginX = typed.Surface.BaseImage.HasOrigin ? typed.Surface.BaseImage.Origin.X : surface.BaseOriginX;
            surface.BaseOriginY = typed.Surface.BaseImage.HasOrigin ? typed.Surface.BaseImage.Origin.Y : surface.BaseOriginY;
        }
        foreach (object entry in GetEnumerable(root, "MapEntries", "Entries", "MapList"))
        {
            var marker = new WorldMapMarkerItem
            {
                NativeKey = GetString(entry, "NativeKey", "Key", "Id") ?? surface.Markers.Count.ToString(CultureInfo.InvariantCulture),
                MarkerType = GetInt(entry, "MarkerType", "Type") ?? 0,
                X = GetInt(GetObject(entry, "Spot", "Position"), "X", "x", "Item1") ?? GetInt(entry, "X") ?? 0,
                Y = GetInt(GetObject(entry, "Spot", "Position"), "Y", "y", "Item2") ?? GetInt(entry, "Y") ?? 0,
                Title = GetString(entry, "Title") ?? string.Empty,
                Description = GetString(entry, "Description", "Desc") ?? string.Empty,
                TownDescription = GetString(entry, "TownDescription") ?? string.Empty,
            };
            marker.SetCanvasOrigin(surface.BaseOriginX, surface.BaseOriginY);
            marker.SetMapIds(GetEnumerable(entry, "MapReferences", "MapIds", "Maps", "MapNo").Select(value => GetInt(value, "MapId", "Id", "Value") ?? ToInt(value)).Where(value => value > 0), preserveDuplicates: true);
            surface.Markers.Add(marker);
        }
        foreach (object link in GetEnumerable(root, "Links", "MapLinks"))
        {
            object? spot = GetObject(link, "Spot", "Position");
            surface.Links.Add(new WorldMapLinkItem
            {
                NativeKey = GetString(link, "NativeKey", "Key") ?? string.Empty,
                TargetName = GetString(link, "TargetLogicalName", "TargetName", "LinkMap") ?? string.Empty,
                Tooltip = GetString(link, "Tooltip", "ToolTip") ?? string.Empty,
                X = GetInt(spot, "X", "x", "Item1") ?? 0,
                Y = GetInt(spot, "Y", "y", "Item2") ?? 0,
            });
        }
        surface.LinkCount = surface.Links.Count;
        foreach (object fog in GetEnumerable(root, "FogLayers", "Fog"))
            surface.FogLayers.Add(new WorldMapFogItem
            {
                NativeKey = GetString(fog, "NativeKey", "Key") ?? surface.FogLayers.Count.ToString(CultureInfo.InvariantCulture),
                Quest = GetInt(fog, "Quest", "QuestId"),
                QState = GetInt(fog, "QState", "qState"),
            });
        surface.FogCount = surface.FogLayers.Count;
        foreach (object diagnostic in GetEnumerable(root, "Diagnostics"))
            surface.Diagnostics.Add(new WorldMapDiagnosticItem { Severity = GetString(diagnostic, "Severity", "Level") ?? "Info", Path = GetString(diagnostic, "Path", "PropertyPath") ?? string.Empty, Message = GetString(diagnostic, "Message", "Text") ?? diagnostic.ToString() ?? string.Empty });
    }

    internal static void ApplyPresentationToDocument(WorldMapSurfaceItem presentation, WorldMapDocument document)
    {
        if (presentation == null || document?.Surface == null) return;
        document.Surface.LogicalName = presentation.LogicalName;
        document.Surface.ParentName = string.IsNullOrWhiteSpace(presentation.ParentName) ? null : presentation.ParentName;
        while (document.Surface.Entries.Count > presentation.Markers.Count)
            document.Surface.RemoveEntry(document.Surface.Entries[^1]);
        for (int i = 0; i < presentation.Markers.Count; i++)
        {
            WorldMapMarkerItem marker = presentation.Markers[i];
            WorldMapMapEntry entry = i < document.Surface.Entries.Count ? document.Surface.Entries[i] : document.Surface.AddEntry(marker.NativeKey);
            entry.Key = marker.NativeKey;
            entry.Type = marker.MarkerType;
            entry.Spot = new System.Drawing.Point(marker.X, marker.Y);
            entry.MapIds.Clear(); foreach (int id in marker.MapIds) entry.MapIds.Add(id);
            entry.Title = string.IsNullOrEmpty(marker.Title) ? null : marker.Title;
            entry.Description = string.IsNullOrEmpty(marker.Description) ? null : marker.Description;
            entry.TownDescription = string.IsNullOrEmpty(marker.TownDescription) ? null : marker.TownDescription;
        }
        while (document.Surface.Links.Count > presentation.Links.Count)
            document.Surface.RemoveLink(document.Surface.Links[^1]);
        for (int i = 0; i < presentation.Links.Count; i++)
        {
            WorldMapLinkItem source = presentation.Links[i];
            WorldMapLink link = i < document.Surface.Links.Count ? document.Surface.Links[i] : document.Surface.AddLink(source.NativeKey);
            link.Key = source.NativeKey;
            link.LinkMap = string.IsNullOrWhiteSpace(source.TargetName) ? null : source.TargetName;
            link.ToolTip = string.IsNullOrWhiteSpace(source.Tooltip) ? null : source.Tooltip;
            link.Spot = new System.Drawing.Point(source.X, source.Y);
        }
        while (document.Surface.FogLayers.Count > presentation.FogLayers.Count)
            document.Surface.RemoveFogLayer(document.Surface.FogLayers[^1]);
        for (int i = 0; i < presentation.FogLayers.Count; i++)
        {
            WorldMapFogItem source = presentation.FogLayers[i];
            WorldMapFogLayer fog = i < document.Surface.FogLayers.Count ? document.Surface.FogLayers[i] : document.Surface.AddFogLayer(source.NativeKey);
            fog.Key = source.NativeKey;
            fog.Quest = source.Quest;
            fog.QState = source.QState;
        }
        document.IsDirty = true;
    }

    private static IEnumerable<object> GetEnumerable(object? value, params string[] names)
    {
        object? property = GetObject(value, names);
        if (property is IEnumerable enumerable && property is not string)
            foreach (object? item in enumerable)
                if (item != null)
                    yield return item;
    }

    private static object? GetObject(object? value, params string[] names)
    {
        if (value == null)
            return null;
        Type type = value.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        PropertyInfo[] properties = type.GetProperties(flags);
        FieldInfo[] fields = type.GetFields(flags);
        foreach (string name in names)
        {
            PropertyInfo? property = properties.FirstOrDefault(candidate =>
                    candidate.GetIndexParameters().Length == 0
                    && string.Equals(candidate.Name, name, StringComparison.Ordinal))
                ?? properties.FirstOrDefault(candidate =>
                    candidate.GetIndexParameters().Length == 0
                    && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                try { return property.GetValue(value); } catch { }
            }
            FieldInfo? field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal))
                ?? fields.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                try { return field.GetValue(value); } catch { }
            }
        }
        return null;
    }

    private static string? GetString(object? value, params string[] names)
    {
        object? result = GetObject(value, names);
        return result?.ToString();
    }

    private static int? GetInt(object? value, params string[] names)
    {
        object? result = GetObject(value, names);
        if (result == null)
            return null;
        return int.TryParse(result.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : null;
    }

    private static int ToInt(object? value) => int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;
}
