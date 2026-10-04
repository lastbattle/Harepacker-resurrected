using HaCreator.MapEditor;
using HaCreator.GUI.InstanceEditor;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.MapStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingColor = System.Drawing.Color;

namespace HaCreator.GUI.Cutscene
{
    public partial class CutsceneWorkspace : Window
    {
        private readonly Board _board;
        private readonly ObservableCollection<CutsceneImageModel> _sceneImages = new();
        private readonly ObservableCollection<CutsceneSceneModel> _visibleScenes = new();
        private readonly ObservableCollection<CutsceneEventModel> _timelineEvents = new();
        private readonly DispatcherTimer _playbackTimer;
        private readonly SoundManager _previewSoundManager = new();
        private readonly HashSet<CutsceneSceneModel> _dirtyScenes = new();
        private readonly Dictionary<WzCanvasProperty, BitmapSource> _visualCache = new();
        private readonly Dictionary<CutsceneEventModel, CharacterPreviewImage> _characterPreviewCache = new();
        private readonly MapDirectionInfo _initialDirectionInfo;
        private readonly bool _initialBoardDirty;
        private readonly BitmapSource _mapPreviewSource;
        private readonly List<CutsceneSceneModel> _allScenes = new();
        private CutsceneImageModel _selectedSceneImage;
        private CutsceneSceneModel _selectedScene;
        private bool _isPlaying;
        private bool _draggingMarker;
        private bool _updatingEventQueue;
        private DateTime _lastTick;
        private double _previewScale = 1;
        private double _screenScale = 1;
        private Point _previewOrigin;
        private bool _hasChanges;
        private bool _syncingSelection;
        private bool _suppressSceneTabSwitch;
        private Line _timelinePlayhead;
        private CutsceneEventModel _draggedTimelineEvent;
        private Border _draggedTimelineBlock;
        private double _timelineDragOffsetX;
        private bool _draggingTimelineEvent;
        private readonly Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> _undoHistory = new();
        private readonly Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> _redoHistory = new();
        private readonly HashSet<CutsceneSceneModel> _newScenes = new();
        private readonly HashSet<CutsceneSceneModel> _deletedScenes = new();
        private CutsceneHistorySnapshot _historySnapshot;
        private CutsceneHistorySnapshot _historyTransactionStart;
        private bool _restoringHistory;

        private const double TimelineRulerHeight = 24;
        private const double TimelineLaneHeight = 24;
        private const double TimelinePixelsPerMillisecond = 0.08;
        private const int MaxHistoryEntries = 100;

        public IReadOnlyList<string> ActionCatalogue { get; } = new[]
        {
            "shoot1", "alert3", "alert5", "magic1", "genesis", "blade", "soulblow", "flameWheel",
            "darkFog", "overSwingDouble", "overSwingTriple", "finalBlow"
        };

        public CutsceneWorkspace(Board board)
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, Undo_Executed, History_CanExecute));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, Redo_Executed, History_CanExecute));
            InputBindings.Add(new System.Windows.Input.KeyBinding(ApplicationCommands.Undo, Key.Z, ModifierKeys.Control));
            InputBindings.Add(new System.Windows.Input.KeyBinding(ApplicationCommands.Redo, Key.Y, ModifierKeys.Control));
            _board = board;
            sceneImageComboBox.ItemsSource = _sceneImages;
            sceneListBox.ItemsSource = _visibleScenes;
            timelineGrid.ItemsSource = _timelineEvents;
            timelineGrid.Tag = Enum.GetValues<ReservedSceneEventType>()
                .Select(value => new Choice((int)value, value.ToString())).ToArray();
            _playbackTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            if (_board != null)
            {
                _board.MapInfo.directionInfo ??= new MapDirectionInfo();
                _initialDirectionInfo = MapDirectionInfo.FromProperty(_board.MapInfo.directionInfo.ToProperty());
                _initialBoardDirty = _board.Dirty;
                if (_board.MiniMap != null)
                {
                    _mapPreviewSource = SelectorDialogSupport.ToBitmapSource(_board.MiniMap);
                    _mapPreviewSource?.Freeze();
                }
                triggerListBox.ItemsSource = _board.MapInfo.directionInfo.Events;
                onUserEnterTextBox.Text = _board.MapInfo.onUserEnter ?? string.Empty;
                onFirstUserEnterTextBox.Text = _board.MapInfo.onFirstUserEnter ?? string.Empty;
            }
            else
            {
                triggerListBox.IsEnabled = false;
                onUserEnterTextBox.IsEnabled = false;
                onFirstUserEnterTextBox.IsEnabled = false;
            }

            Loaded += CutsceneWorkspace_Loaded;
            Closing += CutsceneWorkspace_Closing;
            Closed += CutsceneWorkspace_Closed;
        }

        private void CutsceneWorkspace_Loaded(object sender, RoutedEventArgs e)
        {
            statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_Loading");
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    foreach (CutsceneImageModel image in CutsceneRepository.LoadSceneImageIndex())
                        _sceneImages.Add(image);
                    statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_Ready");
                }
                catch (Exception ex)
                {
                    statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_LoadError", ex.Message);
                }
                RenderPreview();
            }, DispatcherPriority.Background);
        }

        private void SceneSearch_Changed(object sender, TextChangedEventArgs e) => ApplySceneFilter();

        private void AddScene_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSceneImage?.Scenes == null)
                return;

            WzImage image = _selectedSceneImage.ResolveImage();
            if (image == null)
                return;

            WzObject parent = _selectedScene?.Parent ?? image;
            string parentPath = image.Name;
            if (_selectedScene != null)
            {
                int separator = _selectedScene.Path.LastIndexOf('/');
                if (separator >= 0)
                    parentPath = _selectedScene.Path[..separator];
            }
            string name = NextSceneName(parent);
            string defaultPath = $"{parentPath}/{name}";
            CutsceneSceneNameDialog nameDialog = new(defaultPath, ValidateNewScenePath)
            {
                Owner = this
            };
            if (nameDialog.ShowDialog() != true)
                return;
            if (!TryResolveNewScenePath(nameDialog.ScenePath, out parent, out name, out string scenePath, out _))
                return;
            CutsceneSceneModel scene = CutsceneRepository.CreateScene(
                _selectedSceneImage,
                parent,
                name,
                scenePath);
            if (scene == null)
                return;

            _selectedSceneImage.Scenes = _selectedSceneImage.Scenes.Concat(new[] { scene }).ToList();
            _allScenes.Add(scene);
            _newScenes.Add(scene);
            _dirtyScenes.Add(scene);
            _hasChanges = true;
            ApplySceneFilter();
            if (!_visibleScenes.Contains(scene))
            {
                sceneSearchBox.Clear();
                ApplySceneFilter();
            }
            sceneListBox.SelectedItem = scene;
            sceneListBox.ScrollIntoView(scene);
            statusTextBlock.Text = $"Created {scene.Path}. Add or edit its events, then save.";
        }

        private void DeleteScene_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedScene == null)
                return;

            CutsceneSceneModel scene = _selectedScene;
            bool isNewScene = _newScenes.Remove(scene);
            if (!isNewScene)
            {
                _deletedScenes.Add(scene);
                _dirtyScenes.Add(scene);
            }
            else
                _dirtyScenes.Remove(scene);

            if (ReferenceEquals(sceneListBox.SelectedItem, scene))
                sceneListBox.SelectedItem = null;
            foreach (CutsceneImageModel image in _sceneImages)
            {
                if (image.Scenes == null || !image.Scenes.Contains(scene))
                    continue;
                image.Scenes = image.Scenes
                    .Where(existing => !ReferenceEquals(existing, scene))
                    .ToList();
            }
            _visibleScenes.Remove(scene);
            _allScenes.Remove(scene);
            _hasChanges = true;
            sceneExplorerTab.IsSelected = true;
            statusTextBlock.Text = $"Deleted {scene.Path}. Save to persist the change.";
        }

        private void ChooseMap_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CutsceneEventModel eventModel)
                return;

            using var adapter = new Forms.NumericUpDown
            {
                Minimum = int.MinValue,
                Maximum = int.MaxValue,
                Value = eventModel.Field
            };
            LoadMapSelector selector = new(adapter) { Owner = this };
            if (selector.ShowDialog() == true)
                eventModel.Field = (int)adapter.Value;
        }

        private void AddEquipment_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is not CutsceneEventModel eventModel
                || !eventModel.IsCharacterAppearance)
                return;

            LoadItemSelector itemSelector = new(0, InventoryType.EQUIP) { Owner = this };
            itemSelector.ShowDialog();
            if (itemSelector.SelectedItemId == 0)
                return;

            int slot = CutsceneEventModel.GuessEquipmentSlot(itemSelector.SelectedItemId);
            BeginHistoryEdit();
            try
            {
                CutsceneEquipmentEntry existing = eventModel.Equipment.FirstOrDefault(item => item.Slot == slot && slot > 0);
                if (existing != null)
                    existing.ItemId = itemSelector.SelectedItemId;
                else
                {
                    CutsceneEquipmentEntry equipment = new(slot, itemSelector.SelectedItemId);
                    eventModel.Equipment.Add(equipment);
                    equipmentListBox.SelectedItem = equipment;
                }
            }
            finally
            {
                EndHistoryEdit();
            }
        }

        private void DeleteEquipment_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is CutsceneEventModel eventModel
                && (sender as FrameworkElement)?.DataContext is CutsceneEquipmentEntry equipment)
            {
                BeginHistoryEdit();
                try
                {
                    eventModel.Equipment.Remove(equipment);
                }
                finally
                {
                    EndHistoryEdit();
                }
            }
        }

        private void AddUnknownField_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is not CutsceneEventModel eventModel)
                return;

            CutsceneUnknownFieldEntry field = new(string.Empty, string.Empty, null);
            BeginHistoryEdit();
            try
            {
                eventModel.UnknownFields.Add(field);
                unknownFieldsListBox.SelectedItem = field;
            }
            finally
            {
                EndHistoryEdit();
            }
        }

        private void DeleteUnknownField_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is CutsceneEventModel eventModel
                && (sender as FrameworkElement)?.DataContext is CutsceneUnknownFieldEntry field)
            {
                BeginHistoryEdit();
                try
                {
                    eventModel.UnknownFields.Remove(field);
                }
                finally
                {
                    EndHistoryEdit();
                }
            }
        }

        private void RenderCharacterPreview(CutsceneEventModel eventModel)
        {
            characterPreviewImage.Source = null;
            if (eventModel == null || !eventModel.IsCharacterAppearance)
                return;

            try
            {
                characterPreviewImage.Source = GetCharacterPreview(eventModel)?.Source;
            }
            catch
            {
                // Character assets vary considerably between client versions. The
                // equipment editor remains usable when a preview asset is missing.
            }
        }

        private CharacterPreviewImage GetCharacterPreview(CutsceneEventModel eventModel)
        {
            if (eventModel == null || !eventModel.IsCharacterAppearance)
                return null;
            if (_characterPreviewCache.TryGetValue(eventModel, out CharacterPreviewImage cached))
                return cached;

            CharacterPreviewImage preview = BuildCharacterPreview(eventModel);
            _characterPreviewCache[eventModel] = preview;
            return preview;
        }

        private static CharacterPreviewImage BuildCharacterPreview(CutsceneEventModel eventModel)
        {
            List<CharacterPreviewLayer> layers = new();
            try
            {
                WzImage bodyImage = Program.FindImage("Character", "00002000.img");
                WzImage headImage = Program.FindImage("Character", "00012000.img");
                if (bodyImage == null || headImage == null)
                    return null;

                WzImage faceImage = Program.FindImage("Character", "Face/00020000.img");
                WzImage hairImage = Program.FindImage("Character", "Hair/00030000.img");
                IEnumerable<(int ItemId, WzImage Image)> equipmentImages = eventModel.Equipment
                    .Where(item => item.ItemId > 0)
                    .Select(item =>
                    {
                        string folder = CharacterWzComposition.GetEquipmentFolder(item.ItemId);
                        return (item.ItemId, Image: folder == null
                            ? null
                            : Program.FindImage("Character", $"{folder}/{item.ItemId:D8}.img"));
                    });

                IReadOnlyList<CharacterWzLayer> composition = CharacterWzComposition.ComposeStanding(
                    bodyImage,
                    headImage,
                    faceImage,
                    hairImage,
                    equipmentImages);
                foreach (CharacterWzLayer layer in composition)
                {
                    DrawingBitmap bitmap = null;
                    try
                    {
                        bitmap = layer.Canvas.GetLinkedWzCanvasBitmap();
                    }
                    catch
                    {
                        // A missing or malformed canvas should not disable the editor.
                    }

                    if (bitmap != null)
                        layers.Add(new CharacterPreviewLayer(bitmap, layer.X, layer.Y, layer.ZIndex));
                }

                layers.RemoveAll(layer => layer.Bitmap == null);
                if (layers.Count == 0)
                    return null;

                int minX = layers.Min(layer => layer.X);
                int minY = layers.Min(layer => layer.Y);
                int maxX = layers.Max(layer => layer.X + layer.Bitmap.Width);
                int maxY = layers.Max(layer => layer.Y + layer.Bitmap.Height);
                const int padding = 6;
                const int scale = 2;
                using DrawingBitmap output = new(
                    Math.Max(1, (maxX - minX + padding * 2) * scale),
                    Math.Max(1, (maxY - minY + padding * 2) * scale),
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (DrawingGraphics graphics = DrawingGraphics.FromImage(output))
                {
                    graphics.Clear(DrawingColor.Transparent);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    foreach (CharacterPreviewLayer layer in layers.OrderBy(item => item.ZIndex))
                    {
                        graphics.DrawImage(
                            layer.Bitmap,
                            (layer.X - minX + padding) * scale,
                            (layer.Y - minY + padding) * scale,
                            layer.Bitmap.Width * scale,
                            layer.Bitmap.Height * scale);
                    }
                }
                BitmapSource source = SelectorDialogSupport.ToBitmapSource(output);
                source?.Freeze();
                return source == null
                    ? null
                    : new CharacterPreviewImage(source, -minX + padding, -minY + padding);
            }
            finally
            {
                foreach (CharacterPreviewLayer layer in layers)
                    layer.Dispose();
            }
        }

        private void SceneImage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CutsceneImageModel nextImage = sceneImageComboBox.SelectedItem as CutsceneImageModel;
            if (ReferenceEquals(nextImage, _selectedSceneImage))
                return;

            ReleaseSelectedSceneImage();
            _visualCache.Clear();
            _characterPreviewCache.Clear();
            _selectedSceneImage = nextImage;
            _visibleScenes.Clear();
            validationListBox.ItemsSource = null;
            validationListBox.Visibility = Visibility.Collapsed;
            if (nextImage == null)
                return;

            statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_Loading");
            Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(sceneImageComboBox.SelectedItem, nextImage))
                    return;
                try
                {
                    IReadOnlyList<CutsceneSceneModel> scenes = CutsceneRepository.LoadScenes(nextImage);
                    foreach (CutsceneSceneModel scene in scenes.Where(scene => !_allScenes.Contains(scene)))
                        _allScenes.Add(scene);
                    ApplySceneFilter();
                    statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_ScenesLoaded", scenes.Count);
                    if (_visibleScenes.Count > 0)
                    {
                        _suppressSceneTabSwitch = true;
                        try
                        {
                            sceneListBox.SelectedIndex = 0;
                        }
                        finally
                        {
                            _suppressSceneTabSwitch = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    statusTextBlock.Text = CutsceneEditorTextExtension.Get("Cutscene_LoadError", ex.Message);
                }
            }, DispatcherPriority.Background);
        }

        private void ReleaseSelectedSceneImage()
        {
            if (_selectedSceneImage == null || _selectedSceneImage.Scenes == null)
                return;
            if (_selectedSceneImage.Scenes.Any(scene => _dirtyScenes.Contains(scene))
                || _deletedScenes.Any(scene => ReferenceEquals(scene.Image, _selectedSceneImage.Image)))
                return;

            sceneListBox.SelectedItem = null;
            foreach (CutsceneSceneModel scene in _selectedSceneImage.Scenes)
            {
                ClearHistory(scene);
                _allScenes.Remove(scene);
            }
            CutsceneRepository.ReleaseScenes(_selectedSceneImage);
        }

        private void ApplySceneFilter()
        {
            string query = sceneSearchBox.Text?.Trim() ?? string.Empty;
            _visibleScenes.Clear();
            IEnumerable<CutsceneSceneModel> scenes = _selectedSceneImage?.Scenes ?? Array.Empty<CutsceneSceneModel>();
            foreach (CutsceneSceneModel scene in scenes.Where(scene => query.Length == 0 || scene.Path.Contains(query, StringComparison.OrdinalIgnoreCase)))
                _visibleScenes.Add(scene);
        }

        private void Scene_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isPlaying = false;
            _playbackTimer.Stop();
            StopPreviewSounds();
            foreach (CutsceneEventModel cutsceneEvent in _timelineEvents)
                cutsceneEvent.PropertyChanged -= Event_PropertyChanged;
            _selectedScene = sceneListBox.SelectedItem as CutsceneSceneModel;
            _timelineEvents.Clear();
            _characterPreviewCache.Clear();
            RenderCharacterPreview(null);
            if (_selectedScene != null)
            {
                foreach (CutsceneEventModel cutsceneEvent in _selectedScene.Events.OrderBy(item => item.Start).ThenBy(item => ParseIndex(item.Id)))
                {
                    cutsceneEvent.PropertyChanged += Event_PropertyChanged;
                    _timelineEvents.Add(cutsceneEvent);
                }
            }
            UpdateTimelineRange();
            playheadSlider.Value = 0;
            if (_timelineEvents.Count > 0)
                timelineGrid.SelectedIndex = 0;
            if (!_suppressSceneTabSwitch && _selectedScene != null && _timelineEvents.Count > 0)
                selectedEventTab.IsSelected = true;
            RenderTimelineTracks();
            RenderPreview();
            if (!_restoringHistory)
            {
                _historySnapshot = CaptureHistorySnapshot();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void Event_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_restoringHistory)
                return;
            if (_selectedScene != null)
                _dirtyScenes.Add(_selectedScene);
            _hasChanges = true;
            if (sender is CutsceneEventModel changedEvent)
                _characterPreviewCache.Remove(changedEvent);
            UpdateTimelineRange();
            if (!_draggingTimelineEvent)
                RenderTimelineTracks();
            RenderCharacterPreview(sender as CutsceneEventModel);
            RenderPreview();
            RecordHistoryMutation();
        }

        private void Timeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_syncingSelection && timelineGrid.SelectedItem != null)
            {
                _syncingSelection = true;
                triggerListBox.SelectedItem = null;
                _syncingSelection = false;
                if (!_suppressSceneTabSwitch)
                    selectedEventTab.IsSelected = true;
            }
            RenderTimelineTracks();
            RenderCharacterPreview(timelineGrid.SelectedItem as CutsceneEventModel);
            RenderPreview();
            if (!_restoringHistory && _historyTransactionStart == null)
                _historySnapshot = CaptureHistorySnapshot();
        }

        private void Trigger_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_syncingSelection && triggerListBox.SelectedItem != null)
            {
                _syncingSelection = true;
                timelineGrid.SelectedItem = null;
                _syncingSelection = false;
            }
            _updatingEventQueue = true;
            eventQueueTextBox.Text = triggerListBox.SelectedItem is MapDirectionEvent directionEvent
                ? string.Join(Environment.NewLine, directionEvent.EventQueue)
                : string.Empty;
            _updatingEventQueue = false;
            RenderPreview();
        }

        private void EventQueue_Changed(object sender, TextChangedEventArgs e)
        {
            if (_updatingEventQueue || triggerListBox.SelectedItem is not MapDirectionEvent directionEvent)
                return;
            directionEvent.EventQueue.Clear();
            directionEvent.EventQueue.AddRange(eventQueueTextBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
            _hasChanges = true;
        }

        private void AddTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (_board == null)
                return;
            string id = NextId(_board.MapInfo.directionInfo.Events.Select(item => item.Name));
            MapDirectionEvent directionEvent = new() { Name = id };
            _board.MapInfo.directionInfo.Events.Add(directionEvent);
            triggerListBox.Items.Refresh();
            triggerListBox.SelectedItem = directionEvent;
            _board.Dirty = true;
            _hasChanges = true;
            RenderPreview();
        }

        private void DeleteTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (_board == null || triggerListBox.SelectedItem is not MapDirectionEvent directionEvent)
                return;
            _board.MapInfo.directionInfo.Events.Remove(directionEvent);
            triggerListBox.Items.Refresh();
            _board.Dirty = true;
            _hasChanges = true;
            RenderPreview();
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedScene == null)
                return;
            BeginHistoryEdit();
            try
            {
                string id = NextId(_selectedScene.Events.Select(item => item.Id));
                WzSubProperty property = new(id);
                CutsceneEventModel cutsceneEvent = CutsceneEventModel.FromProperty(property);
                cutsceneEvent.Type = (int)ReservedSceneEventType.Visual;
                cutsceneEvent.Start = (int)playheadSlider.Value;
                cutsceneEvent.PropertyChanged += Event_PropertyChanged;
                _selectedScene.Events.Add(cutsceneEvent);
                _dirtyScenes.Add(_selectedScene);
                _hasChanges = true;
                _timelineEvents.Add(cutsceneEvent);
                timelineGrid.SelectedItem = cutsceneEvent;
                UpdateTimelineRange();
                RenderTimelineTracks();
            }
            finally
            {
                EndHistoryEdit();
            }
        }

        private void DuplicateEvent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedScene == null || timelineGrid.SelectedItem is not CutsceneEventModel selected)
                return;
            BeginHistoryEdit();
            try
            {
                string id = NextId(_selectedScene.Events.Select(item => item.Id));
                WzSubProperty clone = (WzSubProperty)selected.Source.DeepClone();
                clone.Name = id;
                CutsceneEventModel duplicate = CutsceneEventModel.FromProperty(clone);
                duplicate.Start = selected.Start + Math.Max(1, selected.Duration);
                duplicate.PropertyChanged += Event_PropertyChanged;
                _selectedScene.Events.Add(duplicate);
                _dirtyScenes.Add(_selectedScene);
                _hasChanges = true;
                _timelineEvents.Add(duplicate);
                timelineGrid.SelectedItem = duplicate;
                UpdateTimelineRange();
                RenderTimelineTracks();
            }
            finally
            {
                EndHistoryEdit();
            }
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedScene == null || timelineGrid.SelectedItem is not CutsceneEventModel selected)
                return;
            BeginHistoryEdit();
            try
            {
                _selectedScene.Events.Remove(selected);
                _dirtyScenes.Add(_selectedScene);
                _hasChanges = true;
                _timelineEvents.Remove(selected);
                UpdateTimelineRange();
                RenderTimelineTracks();
                RenderPreview();
            }
            finally
            {
                EndHistoryEdit();
            }
        }

        private void History_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> history = e.Command == ApplicationCommands.Undo
                ? _undoHistory
                : _redoHistory;
            List<CutsceneHistoryEntry> entries = GetHistory(history, create: false);
            e.CanExecute = entries?.Count > 0;
            e.Handled = e.CanExecute;
        }

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ApplyHistory(_undoHistory, _redoHistory, undo: true))
                e.Handled = true;
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ApplyHistory(_redoHistory, _undoHistory, undo: false))
                e.Handled = true;
        }

        private bool ApplyHistory(
            Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> sourceHistory,
            Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> destinationHistory,
            bool undo)
        {
            List<CutsceneHistoryEntry> sourceEntries = GetHistory(sourceHistory, create: false);
            if (sourceEntries == null || sourceEntries.Count == 0)
                return false;

            CutsceneHistoryEntry entry = sourceEntries[^1];
            RestoreHistorySnapshot(undo ? entry.Before : entry.After);
            sourceEntries.RemoveAt(sourceEntries.Count - 1);
            GetHistory(destinationHistory, create: true).Add(entry);
            CommandManager.InvalidateRequerySuggested();
            return true;
        }

        private List<CutsceneHistoryEntry> GetHistory(
            Dictionary<CutsceneSceneModel, List<CutsceneHistoryEntry>> history,
            bool create)
        {
            if (_selectedScene == null)
                return null;
            if (history.TryGetValue(_selectedScene, out List<CutsceneHistoryEntry> entries))
                return entries;
            if (!create)
                return null;
            entries = new List<CutsceneHistoryEntry>();
            history[_selectedScene] = entries;
            return entries;
        }

        private void BeginHistoryEdit()
        {
            if (_restoringHistory || _selectedScene == null || _historyTransactionStart != null)
                return;
            _historyTransactionStart = CaptureHistorySnapshot();
        }

        private void EndHistoryEdit()
        {
            if (_historyTransactionStart == null)
                return;
            CutsceneHistorySnapshot before = _historyTransactionStart;
            _historyTransactionStart = null;
            CommitHistoryMutation(before);
        }

        private void RecordHistoryMutation()
        {
            if (_restoringHistory || _historyTransactionStart != null || _selectedScene == null)
                return;
            CutsceneHistorySnapshot before = _historySnapshot;
            if (before == null || !ReferenceEquals(before.Scene, _selectedScene))
                before = CaptureHistorySnapshot();
            CommitHistoryMutation(before);
        }

        private void CommitHistoryMutation(CutsceneHistorySnapshot before)
        {
            CutsceneHistorySnapshot after = CaptureHistorySnapshot();
            _historySnapshot = after;
            if (HistoryContentEquals(before, after))
                return;

            List<CutsceneHistoryEntry> undoEntries = GetHistory(_undoHistory, create: true);
            undoEntries.Add(new CutsceneHistoryEntry(before, after));
            if (undoEntries.Count > MaxHistoryEntries)
                undoEntries.RemoveAt(0);
            GetHistory(_redoHistory, create: true).Clear();
            CommandManager.InvalidateRequerySuggested();
        }

        private CutsceneHistorySnapshot CaptureHistorySnapshot()
        {
            return new CutsceneHistorySnapshot(
                _selectedScene,
                _selectedScene?.Events.Select(CutsceneEventHistorySnapshot.FromModel).ToList() ?? new List<CutsceneEventHistorySnapshot>(),
                (timelineGrid.SelectedItem as CutsceneEventModel)?.Id);
        }

        private static bool HistoryContentEquals(CutsceneHistorySnapshot left, CutsceneHistorySnapshot right)
        {
            if (left == null || right == null)
                return ReferenceEquals(left, right);
            if (!ReferenceEquals(left?.Scene, right?.Scene)
                || left.Events.Count != right.Events.Count)
                return false;
            return left.Events.Zip(right.Events, (before, after) => before.ContentEquals(after)).All(equal => equal);
        }

        private void RestoreHistorySnapshot(CutsceneHistorySnapshot snapshot)
        {
            if (snapshot?.Scene == null)
                return;

            _restoringHistory = true;
            try
            {
                CutsceneSceneModel scene = snapshot.Scene;
                List<CutsceneEventModel> available = scene.Events.ToList();
                List<CutsceneEventModel> restored = new();
                foreach (CutsceneEventHistorySnapshot eventSnapshot in snapshot.Events)
                {
                    int existingIndex = available.FindIndex(item => string.Equals(item.Id, eventSnapshot.Id, StringComparison.Ordinal));
                    CutsceneEventModel eventModel;
                    if (existingIndex >= 0)
                    {
                        eventModel = available[existingIndex];
                        available.RemoveAt(existingIndex);
                        eventSnapshot.ApplyTo(eventModel);
                    }
                    else
                        eventModel = eventSnapshot.CreateModel();
                    restored.Add(eventModel);
                }

                foreach (CutsceneEventModel eventModel in _timelineEvents)
                    eventModel.PropertyChanged -= Event_PropertyChanged;
                scene.Events.Clear();
                scene.Events.AddRange(restored);
                _selectedScene = scene;

                _timelineEvents.Clear();
                foreach (CutsceneEventModel eventModel in scene.Events
                    .OrderBy(item => item.Start)
                    .ThenBy(item => ParseIndex(item.Id)))
                {
                    eventModel.PropertyChanged += Event_PropertyChanged;
                    _timelineEvents.Add(eventModel);
                }

                _characterPreviewCache.Clear();
                UpdateTimelineRange();
                timelineGrid.SelectedItem = snapshot.SelectedEventId == null
                    ? null
                    : _timelineEvents.FirstOrDefault(item => string.Equals(item.Id, snapshot.SelectedEventId, StringComparison.Ordinal));
                if (timelineGrid.SelectedItem != null)
                    timelineGrid.ScrollIntoView(timelineGrid.SelectedItem);
                RenderTimelineTracks();
                RenderCharacterPreview(timelineGrid.SelectedItem as CutsceneEventModel);
                RenderPreview();
                _dirtyScenes.Add(scene);
                _hasChanges = true;
            }
            finally
            {
                _restoringHistory = false;
            }

            _historySnapshot = CaptureHistorySnapshot();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAllChanges();
        }

        private void ValidateScene_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<CutsceneValidationIssue> issues = ValidateWorkspace(
                _selectedScene == null ? Array.Empty<CutsceneSceneModel>() : new[] { _selectedScene },
                includeTriggers: false);
            ShowValidationResults(issues, CutsceneEditorTextExtension.Get("Cutscene_ValidateScene"));
        }

        private void ValidateAll_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<CutsceneValidationIssue> issues = ValidateWorkspace(_allScenes, includeTriggers: true);
            ShowValidationResults(issues, CutsceneEditorTextExtension.Get("Cutscene_ValidateAll"));
        }

        private void BrowseVisual_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is not CutsceneEventModel selected)
                return;
            CutsceneAssetPicker picker = new(
                _allScenes.SelectMany(scene => scene.Events).Select(item => item.Visual),
                CutsceneAssetKind.Visual,
                ResolveVisualPreview)
            {
                Owner = this
            };
            if (picker.ShowDialog() == true)
                selected.Visual = picker.SelectedPath;
        }

        private void BrowseSound_Click(object sender, RoutedEventArgs e)
        {
            if (timelineGrid.SelectedItem is not CutsceneEventModel selected)
                return;
            IReadOnlyList<string> soundImages = CutsceneRepository.LoadSoundImageIndex();
            CutsceneAssetPicker picker = new(
                soundImages,
                CutsceneRepository.LoadSoundPaths,
                GetSoundImagePath(selected.Sound),
                CutsceneAssetKind.Sound)
            {
                Owner = this
            };
            if (picker.ShowDialog() == true)
                selected.Sound = picker.SelectedPath;
        }

        private static string GetSoundImagePath(string soundPath)
        {
            string[] segments = (soundPath ?? string.Empty).Replace('\\', '/').Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            int start = segments.Length > 0 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            for (int index = start; index < segments.Length; index++)
            {
                if (segments[index].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    return string.Join('/', segments.Skip(start).Take(index - start + 1));
            }
            return segments.Length > start ? segments[start] + ".img" : null;
        }

        private bool SaveAllChanges()
        {
            IReadOnlyList<CutsceneValidationIssue> issues = ValidateWorkspace(
                _dirtyScenes.Where(scene => !_deletedScenes.Contains(scene)),
                includeTriggers: true);
            if (issues.Any(issue => issue.Severity == CutsceneValidationSeverity.Error))
            {
                ShowValidationResults(issues, CutsceneEditorTextExtension.Get("Cutscene_SaveBlocked"));
                return false;
            }
            foreach (CutsceneSceneModel scene in _dirtyScenes.Where(scene => !_deletedScenes.Contains(scene)).ToList())
                CutsceneRepository.SaveScene(scene);
            foreach (CutsceneSceneModel scene in _deletedScenes.ToList())
                CutsceneRepository.DeleteScene(scene);
            if (_board != null)
            {
                _board.MapInfo.onUserEnter = EmptyToNull(onUserEnterTextBox.Text);
                _board.MapInfo.onFirstUserEnter = EmptyToNull(onFirstUserEnterTextBox.Text);
                _board.Dirty = true;
            }
            statusTextBlock.Text = issues.Count == 0
                ? CutsceneEditorTextExtension.Get("Cutscene_Saved")
                : CutsceneEditorTextExtension.Get("Cutscene_ValidationSummary", issues.Count, string.Join(" · ", issues.Take(3).Select(issue => issue.Message)));
            validationListBox.ItemsSource = issues;
            validationListBox.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            _dirtyScenes.Clear();
            _newScenes.Clear();
            _deletedScenes.Clear();
            _hasChanges = false;
            return true;
        }

        private void TriggerBinding_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
                _hasChanges = true;
        }

        private void TriggerProperty_Changed(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || triggerListBox.SelectedItem == null)
                return;
            _hasChanges = true;
            if (_board != null)
                _board.Dirty = true;
            Dispatcher.BeginInvoke(RenderPreview, DispatcherPriority.Background);
        }

        private void CutsceneWorkspace_Closing(object sender, CancelEventArgs e)
        {
            if (!_hasChanges)
                return;
            MessageBoxResult result = MessageBox.Show(this,
                CutsceneEditorTextExtension.Get("Cutscene_UnsavedPrompt"),
                CutsceneEditorTextExtension.Get("Cutscene_UnsavedTitle"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == MessageBoxResult.Yes)
            {
                if (!SaveAllChanges())
                    e.Cancel = true;
                return;
            }
            DiscardNewScenes();
            if (_board != null)
            {
                _board.MapInfo.directionInfo = MapDirectionInfo.FromProperty(_initialDirectionInfo.ToProperty());
                _board.Dirty = _initialBoardDirty;
            }
        }

        private void DiscardNewScenes()
        {
            foreach (CutsceneSceneModel scene in _newScenes.ToList())
            {
                _allScenes.Remove(scene);
                foreach (CutsceneImageModel image in _sceneImages)
                {
                    if (image.Scenes == null || !image.Scenes.Contains(scene))
                        continue;
                    image.Scenes = image.Scenes
                        .Where(existing => !ReferenceEquals(existing, scene))
                        .ToList();
                }
                ClearHistory(scene);
                if (ReferenceEquals(_selectedScene, scene))
                    _selectedScene = null;
            }
            _newScenes.Clear();
        }

        private void CutsceneWorkspace_Closed(object sender, EventArgs e)
        {
            _playbackTimer.Stop();
            _previewSoundManager.Dispose();
            _visualCache.Clear();
            _characterPreviewCache.Clear();
            foreach (CutsceneImageModel image in _sceneImages.Where(image => image.Scenes != null
                && !image.Scenes.Any(scene => _dirtyScenes.Contains(scene))))
                CutsceneRepository.ReleaseScenes(image);
        }

        private IReadOnlyList<CutsceneValidationIssue> ValidateWorkspace(IEnumerable<CutsceneSceneModel> scenes, bool includeTriggers)
        {
            List<CutsceneValidationIssue> issues = CutsceneValidator.ValidateScenes(
                scenes,
                (scene, path) => TryResolveVisual(scene.Image, path, 0, out _),
                SoundPathExists).ToList();
            if (includeTriggers && _board != null)
                issues.AddRange(CutsceneValidator.ValidateTriggers(_board.MapInfo.directionInfo, _board.MapSize.X, _board.MapSize.Y));
            foreach (CutsceneValidationIssue issue in issues)
                issue.Message = FormatValidationIssue(issue);
            return issues;
        }

        private string FormatValidationIssue(CutsceneValidationIssue issue)
        {
            string eventId = issue.Event?.Id ?? string.Empty;
            string message = issue.Code switch
            {
                CutsceneValidationCode.UnsupportedType => CutsceneEditorTextExtension.Get("Cutscene_UnsupportedType", eventId, issue.Event.Type),
                CutsceneValidationCode.MissingVisual => CutsceneEditorTextExtension.Get("Cutscene_MissingVisual", eventId),
                CutsceneValidationCode.MissingSound => CutsceneEditorTextExtension.Get("Cutscene_MissingSound", eventId),
                CutsceneValidationCode.InvalidField => CutsceneEditorTextExtension.Get("Cutscene_InvalidField", eventId),
                CutsceneValidationCode.MissingAction => CutsceneEditorTextExtension.Get("Cutscene_MissingAction", eventId),
                CutsceneValidationCode.MissingAppearance => CutsceneEditorTextExtension.Get("Cutscene_MissingAppearance", eventId),
                CutsceneValidationCode.InvalidMotionDuration => CutsceneEditorTextExtension.Get("Cutscene_InvalidMotionDuration", eventId),
                CutsceneValidationCode.NegativeStart => CutsceneEditorTextExtension.Get("Cutscene_NegativeStart", eventId),
                CutsceneValidationCode.NegativeDuration => CutsceneEditorTextExtension.Get("Cutscene_NegativeDuration", eventId),
                CutsceneValidationCode.DuplicateEventId => CutsceneEditorTextExtension.Get("Cutscene_DuplicateEvent", eventId),
                CutsceneValidationCode.TriggerOutsideMap => CutsceneEditorTextExtension.Get("Cutscene_TriggerOutside", issue.Trigger.Name),
                CutsceneValidationCode.DuplicateTriggerId => CutsceneEditorTextExtension.Get("Cutscene_DuplicateTrigger", issue.Trigger.Name),
                _ => issue.Code.ToString()
            };
            return issue.Scene == null ? message : $"{issue.Scene.Path}: {message}";
        }

        private void ShowValidationResults(IReadOnlyList<CutsceneValidationIssue> issues, string scope)
        {
            validationListBox.ItemsSource = issues;
            validationListBox.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            statusTextBlock.Text = issues.Count == 0
                ? CutsceneEditorTextExtension.Get("Cutscene_ValidationPassed", scope)
                : CutsceneEditorTextExtension.Get("Cutscene_ValidationFound", scope, issues.Count,
                    issues.Count(issue => issue.Severity == CutsceneValidationSeverity.Error));
        }

        private void ValidationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (validationListBox.SelectedItem is not CutsceneValidationIssue issue)
                return;
            if (issue.Scene != null)
            {
                if (!_visibleScenes.Contains(issue.Scene))
                    sceneSearchBox.Text = string.Empty;
                sceneListBox.SelectedItem = issue.Scene;
                sceneListBox.ScrollIntoView(issue.Scene);
                if (issue.Event != null)
                {
                    timelineGrid.SelectedItem = issue.Event;
                    timelineGrid.ScrollIntoView(issue.Event);
                    playheadSlider.Value = Math.Max(0, issue.Event.Start);
                }
            }
            if (issue.Trigger != null)
            {
                triggerListBox.SelectedItem = issue.Trigger;
                triggerListBox.ScrollIntoView(issue.Trigger);
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (playheadSlider.Value >= playheadSlider.Maximum)
                playheadSlider.Value = 0;
            StopPreviewSounds();
            PlayPreviewSoundsAt(playheadSlider.Value);
            _isPlaying = true;
            _lastTick = DateTime.UtcNow;
            _playbackTimer.Start();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            _playbackTimer.Stop();
            StopPreviewSounds();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Pause_Click(sender, e);
            playheadSlider.Value = 0;
        }

        private void Step_Click(object sender, RoutedEventArgs e) => playheadSlider.Value = Math.Min(playheadSlider.Maximum, playheadSlider.Value + 16);

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPlaying)
                return;
            DateTime now = DateTime.UtcNow;
            double previousPosition = playheadSlider.Value;
            double nextPosition = Math.Min(playheadSlider.Maximum, previousPosition + (now - _lastTick).TotalMilliseconds);
            PlayPreviewSoundsBetween(previousPosition, nextPosition);
            playheadSlider.Value = nextPosition;
            _previewSoundManager.Update();
            _lastTick = now;
            if (playheadSlider.Value >= playheadSlider.Maximum)
            {
                if (loopCheckBox.IsChecked == true)
                {
                    StopPreviewSounds();
                    playheadSlider.Value = 0;
                    PlayPreviewSoundsAt(0);
                }
                else
                {
                    Pause_Click(this, new RoutedEventArgs());
                    playheadSlider.Value = 0;
                }
            }
        }

        private void Playhead_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            previewCaption.Text = $"{e.NewValue:0} ms · {_selectedScene?.Path ?? CutsceneEditorTextExtension.Get("Cutscene_NoScene")}";
            CutsceneEventModel active = CutscenePlaybackTiming.FindReachedEvent(_timelineEvents, e.NewValue);
            if (active != null && !ReferenceEquals(timelineGrid.SelectedItem, active))
            {
                timelineGrid.SelectedItem = active;
                timelineGrid.ScrollIntoView(active);
            }
            UpdateTimelinePlayhead();
            RenderPreview();
        }

        private void Playhead_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => StopPreviewSounds();

        private void PlayPreviewSoundsAt(double position)
        {
            foreach (CutsceneEventModel cutsceneEvent in _timelineEvents.Where(item => Math.Abs(item.Start - position) < 0.5))
                PlayPreviewSound(cutsceneEvent.Sound);
        }

        private void PlayPreviewSoundsBetween(double previousPosition, double nextPosition)
        {
            foreach (CutsceneEventModel cutsceneEvent in _timelineEvents
                .Where(item => item.Start > previousPosition && item.Start <= nextPosition)
                .OrderBy(item => item.Start)
                .ThenBy(item => ParseIndex(item.Id)))
                PlayPreviewSound(cutsceneEvent.Sound);
        }

        private void PlayPreviewSound(string soundPath)
        {
            if (string.IsNullOrWhiteSpace(soundPath))
                return;
            WzBinaryProperty sound = ResolveSoundPath(soundPath);
            if (sound == null)
                return;
            _previewSoundManager.RegisterSound(soundPath, sound);
            _previewSoundManager.PlaySound(soundPath);
        }

        private void StopPreviewSounds()
        {
            _previewSoundManager.StopAll();
            _previewSoundManager.Update();
        }

        private void UpdateTimelineRange()
        {
            int sceneEnd = CutscenePlaybackTiming.GetSceneEnd(_timelineEvents);
            foreach (CutsceneEventModel cutsceneEvent in _timelineEvents.Where(item => !string.IsNullOrWhiteSpace(item.Sound)))
            {
                WzBinaryProperty sound = ResolveSoundPath(cutsceneEvent.Sound);
                if (sound != null)
                    sceneEnd = Math.Max(sceneEnd, (int)Math.Clamp((long)cutsceneEvent.Start + sound.Length, 0, int.MaxValue));
            }
            playheadSlider.Maximum = sceneEnd;
        }

        private void TimelineViewport_SizeChanged(object sender, SizeChangedEventArgs e) => RenderTimelineTracks();

        private void RenderTimelineTracks()
        {
            if (timelineTrackCanvas == null || timelineTrackLabels == null || timelineScrollViewer == null)
                return;

            TimelineTrack[] tracks = GetTimelineTracks();
            double canvasWidth = Math.Max(timelineScrollViewer.ViewportWidth, playheadSlider.Maximum * TimelinePixelsPerMillisecond + 80);
            double canvasHeight = TimelineRulerHeight + tracks.Length * TimelineLaneHeight;
            timelineTrackCanvas.Width = Math.Max(1, canvasWidth);
            timelineTrackCanvas.Height = canvasHeight;
            timelineTrackCanvas.Children.Clear();
            timelineTrackLabels.Children.Clear();

            timelineTrackLabels.Children.Add(new Border
            {
                Height = TimelineRulerHeight,
                BorderBrush = GetThemeBrush("HareBorderBrush", Brushes.LightGray),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = CutsceneEditorTextExtension.Get("Cutscene_TimeRuler"),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GetThemeBrush("HareMutedTextBrush", Brushes.DimGray)
                }
            });

            for (int index = 0; index < tracks.Length; index++)
            {
                Brush laneBackground = index % 2 == 0 ? Brushes.White : GetThemeBrush("HareCanvasBrush", Brushes.WhiteSmoke);
                double laneTop = TimelineRulerHeight + index * TimelineLaneHeight;
                Rectangle lane = new()
                {
                    Width = canvasWidth,
                    Height = TimelineLaneHeight,
                    Fill = laneBackground,
                    Stroke = GetThemeBrush("HareBorderBrush", Brushes.LightGray),
                    StrokeThickness = 0.5,
                    IsHitTestVisible = false
                };
                timelineTrackCanvas.Children.Add(lane);
                Canvas.SetTop(lane, laneTop);

                timelineTrackLabels.Children.Add(new Border
                {
                    Height = TimelineLaneHeight,
                    BorderBrush = GetThemeBrush("HareBorderBrush", Brushes.LightGray),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = laneBackground,
                    Child = new TextBlock
                    {
                        Text = tracks[index].Name,
                        Margin = new Thickness(8, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                });
            }

            int tickInterval = playheadSlider.Maximum > 60000 ? 5000 : 1000;
            for (int time = 0; time <= playheadSlider.Maximum; time += tickInterval)
            {
                double x = time * TimelinePixelsPerMillisecond;
                Line tick = new()
                {
                    X1 = x,
                    X2 = x,
                    Y1 = TimelineRulerHeight - 5,
                    Y2 = canvasHeight,
                    Stroke = GetThemeBrush("HareBorderBrush", Brushes.LightGray),
                    StrokeThickness = time == 0 ? 1.5 : 0.75,
                    IsHitTestVisible = false
                };
                timelineTrackCanvas.Children.Add(tick);
                TextBlock label = new()
                {
                    Text = $"{time / 1000d:0.#}s",
                    FontSize = 10,
                    Foreground = GetThemeBrush("HareMutedTextBrush", Brushes.DimGray),
                    IsHitTestVisible = false
                };
                timelineTrackCanvas.Children.Add(label);
                Canvas.SetLeft(label, x + 3);
                Canvas.SetTop(label, 3);
            }

            foreach (CutsceneEventModel item in _timelineEvents.OrderBy(item => item.Start).ThenBy(item => ParseIndex(item.Id)))
            {
                int trackIndex = Array.FindIndex(tracks, track => track.Matches(item));
                if (trackIndex < 0)
                    trackIndex = tracks.Length - 1;
                bool selected = ReferenceEquals(timelineGrid.SelectedItem, item);
                double left = item.Start * TimelinePixelsPerMillisecond;
                int effectiveEnd = GetTimelineEventEnd(item);
                double width = Math.Max(12, Math.Max(effectiveEnd - item.Start, 150) * TimelinePixelsPerMillisecond);
                Border block = new()
                {
                    Tag = item,
                    Width = width,
                    Height = TimelineLaneHeight - 6,
                    CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    BorderBrush = selected ? Brushes.White : Brushes.Transparent,
                    Background = GetTrackBrush(trackIndex),
                ToolTip = $"{tracks[trackIndex].Name}: {item.Summary}\n{item.Start}–{effectiveEnd} ms",
                    Child = new TextBlock
                    {
                        Text = item.Summary,
                        Margin = new Thickness(4, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.White,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        IsHitTestVisible = false
                    }
                };
                block.MouseLeftButtonDown += TimelineBlock_MouseLeftButtonDown;
                timelineTrackCanvas.Children.Add(block);
                Canvas.SetLeft(block, left);
                Canvas.SetTop(block, TimelineRulerHeight + trackIndex * TimelineLaneHeight + 3);
            }

            _timelinePlayhead = new Line
            {
                Y1 = 0,
                Y2 = canvasHeight,
                Stroke = GetThemeBrush("HareAccentBrush", Brushes.DodgerBlue),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            timelineTrackCanvas.Children.Add(_timelinePlayhead);
            UpdateTimelinePlayhead();
        }

        private int GetTimelineEventEnd(CutsceneEventModel cutsceneEvent)
        {
            if (cutsceneEvent.Type == (int)ReservedSceneEventType.Sound)
            {
                WzBinaryProperty sound = ResolveSoundPath(cutsceneEvent.Sound);
                if (sound != null)
                    return (int)Math.Clamp((long)cutsceneEvent.Start + sound.Length, 0, int.MaxValue);
            }

            return cutsceneEvent.Type == (int)ReservedSceneEventType.Visual
                ? CutscenePlaybackTiming.GetVisualEnd(_timelineEvents, cutsceneEvent)
                : CutscenePlaybackTiming.GetEffectiveEnd(_timelineEvents, cutsceneEvent);
        }

        private void TimelineBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border { Tag: CutsceneEventModel item } block)
                return;
            timelineGrid.SelectedItem = item;
            playheadSlider.Value = item.Start;
            timelineGrid.ScrollIntoView(item);
            BeginHistoryEdit();
            _draggedTimelineEvent = item;
            _draggedTimelineBlock = block;
            _timelineDragOffsetX = e.GetPosition(block).X;
            _draggingTimelineEvent = true;
            timelineTrackCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            playheadSlider.Value = Math.Clamp(e.GetPosition(timelineTrackCanvas).X / TimelinePixelsPerMillisecond, 0, playheadSlider.Maximum);
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_draggingTimelineEvent || e.LeftButton != MouseButtonState.Pressed || _draggedTimelineEvent == null)
                return;
            double rawStart = (e.GetPosition(timelineTrackCanvas).X - _timelineDragOffsetX) / TimelinePixelsPerMillisecond;
            int snappedStart = Math.Max(0, (int)Math.Round(rawStart / 10) * 10);
            _draggedTimelineEvent.Start = snappedStart;
            playheadSlider.Value = Math.Min(playheadSlider.Maximum, snappedStart);
            if (_draggedTimelineBlock != null)
                Canvas.SetLeft(_draggedTimelineBlock, snappedStart * TimelinePixelsPerMillisecond);
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingTimelineEvent)
                return;
            _draggingTimelineEvent = false;
            _draggedTimelineEvent = null;
            _draggedTimelineBlock = null;
            timelineTrackCanvas.ReleaseMouseCapture();
            RenderTimelineTracks();
            EndHistoryEdit();
            e.Handled = true;
        }

        private void UpdateTimelinePlayhead()
        {
            if (_timelinePlayhead == null || timelineScrollViewer == null)
                return;
            double x = playheadSlider.Value * TimelinePixelsPerMillisecond;
            _timelinePlayhead.X1 = x;
            _timelinePlayhead.X2 = x;
            if (_isPlaying && timelineScrollViewer.ViewportWidth > 0
                && (x < timelineScrollViewer.HorizontalOffset || x > timelineScrollViewer.HorizontalOffset + timelineScrollViewer.ViewportWidth - 24))
                timelineScrollViewer.ScrollToHorizontalOffset(Math.Max(0, x - timelineScrollViewer.ViewportWidth * 0.2));
        }

        private TimelineTrack[] GetTimelineTracks() => new[]
        {
            new TimelineTrack(CutsceneEditorTextExtension.Get("Cutscene_TrackVisual"), item => item.Type == (int)ReservedSceneEventType.Visual),
            new TimelineTrack(CutsceneEditorTextExtension.Get("Cutscene_TrackCharacter"), item => item.Type is (int)ReservedSceneEventType.CharacterAppearance or (int)ReservedSceneEventType.CharacterAction or (int)ReservedSceneEventType.FacialExpression),
            new TimelineTrack(CutsceneEditorTextExtension.Get("Cutscene_TrackSound"), item => item.Type == (int)ReservedSceneEventType.Sound),
            new TimelineTrack(CutsceneEditorTextExtension.Get("Cutscene_TrackTransition"), item => item.Type == (int)ReservedSceneEventType.FieldTransition),
            new TimelineTrack(CutsceneEditorTextExtension.Get("Cutscene_TrackRaw"), item => !Enum.IsDefined(typeof(ReservedSceneEventType), item.Type))
        };

        private static Brush GetTrackBrush(int trackIndex) => trackIndex switch
        {
            0 => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            1 => new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            2 => new SolidColorBrush(Color.FromRgb(5, 150, 105)),
            3 => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
        };

        private Brush GetThemeBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

        private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) => RenderPreview();
        private void Resolution_Changed(object sender, SelectionChangedEventArgs e) => RenderPreview();

        private void RenderPreview()
        {
            if (previewCanvas == null || previewCanvas.ActualWidth <= 0 || previewCanvas.ActualHeight <= 0)
                return;
            previewCanvas.Children.Clear();
            (double frameWidth, double frameHeight) = GetResolution();
            double frameScale = Math.Min((previewCanvas.ActualWidth - 40) / frameWidth, (previewCanvas.ActualHeight - 40) / frameHeight);
            _screenScale = frameScale;
            double width = frameWidth * frameScale;
            double height = frameHeight * frameScale;
            double left = (previewCanvas.ActualWidth - width) / 2;
            double top = (previewCanvas.ActualHeight - height) / 2;
            Rectangle frame = new() { Width = width, Height = height, Stroke = Brushes.SlateGray, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromRgb(31, 41, 55)) };
            previewCanvas.Children.Add(frame);
            Canvas.SetLeft(frame, left);
            Canvas.SetTop(frame, top);
            _previewScale = _board == null ? frameScale : Math.Min(width / Math.Max(_board.MapSize.X, 1), height / Math.Max(_board.MapSize.Y, 1));
            _previewScale = Math.Max(0.05, _previewScale);
            _previewOrigin = new Point(left + width / 2, top + height / 2);

            DrawMapBackground();
            DrawAxis(left, top, width, height);
            DrawActiveCharacter();
            foreach (MapDirectionEvent trigger in _board?.MapInfo.directionInfo?.Events ?? Enumerable.Empty<MapDirectionEvent>())
                DrawMarker(ToMapCanvas(trigger.X, trigger.Y), Brushes.Gold, trigger.Name, trigger.X, trigger.Y, ReferenceEquals(triggerListBox.SelectedItem, trigger));
            foreach (CutsceneEventModel item in _timelineEvents.Where(item => item.Type is 0 or 6))
            {
                if (item.X1 != 0 || item.Y1 != 0)
                    DrawMovement(item.X, item.Y, item.X1, item.Y1);
                DrawMarker(ToScreenCanvas(item.X, item.Y), Brushes.DeepSkyBlue, item.Id, item.X, item.Y, ReferenceEquals(timelineGrid.SelectedItem, item));
            }
            DrawActiveVisuals();
        }

        private void DrawActiveCharacter()
        {
            CutsceneEventModel appearance = _timelineEvents
                .Where(item => item.IsCharacterAppearance && item.Start <= playheadSlider.Value)
                .OrderBy(item => item.Start)
                .ThenBy(item => ParseIndex(item.Id))
                .LastOrDefault();
            if (appearance == null && timelineGrid.SelectedItem is CutsceneEventModel selectedAppearance
                && selectedAppearance.IsCharacterAppearance)
            {
                appearance = selectedAppearance;
            }

            CharacterPreviewImage character = GetCharacterPreview(appearance);
            if (character?.Source == null)
                return;

            const double SourceScale = 2;
            double characterScale = _screenScale / SourceScale;
            (int characterX, int characterY) = CutscenePlaybackTiming.FindCharacterPosition(_timelineEvents, appearance);
            Point position = ToScreenCanvas(characterX, characterY);
            System.Windows.Controls.Image image = new()
            {
                Source = character.Source,
                Width = character.Source.PixelWidth * characterScale,
                Height = character.Source.PixelHeight * characterScale,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                Opacity = 0.98
            };
            previewCanvas.Children.Add(image);
            Canvas.SetLeft(image, position.X - character.AnchorX * characterScale);
            Canvas.SetTop(image, position.Y - character.AnchorY * characterScale);
        }

        private void DrawAxis(double left, double top, double width, double height)
        {
            previewCanvas.Children.Add(new Line { X1 = left, X2 = left + width, Y1 = _previewOrigin.Y, Y2 = _previewOrigin.Y, Stroke = Brushes.DimGray, StrokeThickness = 1 });
            previewCanvas.Children.Add(new Line { X1 = _previewOrigin.X, X2 = _previewOrigin.X, Y1 = top, Y2 = top + height, Stroke = Brushes.DimGray, StrokeThickness = 1 });
        }

        private void DrawMapBackground()
        {
            if (_mapPreviewSource == null || _board == null)
                return;
            double width = Math.Max(1, _board.MapSize.X * _previewScale);
            double height = Math.Max(1, _board.MapSize.Y * _previewScale);
            System.Windows.Controls.Image mapImage = new()
            {
                Source = _mapPreviewSource,
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                Opacity = 0.48,
                IsHitTestVisible = false
            };
            previewCanvas.Children.Add(mapImage);
            Canvas.SetLeft(mapImage, _previewOrigin.X - width / 2);
            Canvas.SetTop(mapImage, _previewOrigin.Y - height / 2);
        }

        private void DrawMovement(int x, int y, int x1, int y1)
        {
            Point start = ToScreenCanvas(x, y);
            Point end = ToScreenCanvas(x1, y1);
            previewCanvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y, Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 } });
        }

        private void DrawMarker(Point point, Brush brush, string label, int x, int y, bool selected)
        {
            Ellipse marker = new() { Width = selected ? 18 : 13, Height = selected ? 18 : 13, Fill = brush, Stroke = selected ? Brushes.White : Brushes.Black, StrokeThickness = selected ? 3 : 1, ToolTip = $"{label}: {x}, {y}" };
            previewCanvas.Children.Add(marker);
            Canvas.SetLeft(marker, point.X - marker.Width / 2);
            Canvas.SetTop(marker, point.Y - marker.Height / 2);
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (timelineGrid.SelectedItem is null && triggerListBox.SelectedItem is null)
                return;
            BeginHistoryEdit();
            _draggingMarker = true;
            previewCanvas.CaptureMouse();
            UpdateDraggedPosition(e.GetPosition(previewCanvas));
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingMarker && e.LeftButton == MouseButtonState.Pressed)
                UpdateDraggedPosition(e.GetPosition(previewCanvas));
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingMarker)
                return;
            _draggingMarker = false;
            previewCanvas.ReleaseMouseCapture();
            EndHistoryEdit();
        }

        private void UpdateDraggedPosition(Point point)
        {
            if (timelineGrid.SelectedItem is CutsceneEventModel cutsceneEvent)
            {
                cutsceneEvent.X = (int)Math.Round((point.X - _previewOrigin.X) / _screenScale);
                cutsceneEvent.Y = (int)Math.Round((point.Y - _previewOrigin.Y) / _screenScale);
            }
            else if (triggerListBox.SelectedItem is MapDirectionEvent directionEvent)
            {
                directionEvent.X = (int)Math.Round((point.X - _previewOrigin.X) / _previewScale);
                directionEvent.Y = (int)Math.Round((point.Y - _previewOrigin.Y) / _previewScale);
                _board.Dirty = true;
                _hasChanges = true;
                RenderPreview();
            }
        }

        private Point ToMapCanvas(int x, int y) => new(_previewOrigin.X + x * _previewScale, _previewOrigin.Y + y * _previewScale);
        private Point ToScreenCanvas(int x, int y) => new(_previewOrigin.X + x * _screenScale, _previewOrigin.Y + y * _screenScale);

        private void DrawActiveVisuals()
        {
            foreach (CutsceneEventModel active in CutscenePlaybackTiming.FindActiveVisuals(_timelineEvents, playheadSlider.Value))
            {
                int elapsed = Math.Max(0, (int)playheadSlider.Value - active.Start);
                if (!TryResolveVisual(active.Visual, elapsed, out BitmapSource source, out Point canvasOrigin))
                    continue;
                double progress = active.Duration > 0 && (active.X1 != 0 || active.Y1 != 0)
                    ? Math.Clamp((double)elapsed / active.Duration, 0, 1)
                    : 0;
                int previewX = (int)Math.Round(active.X + (active.X1 - active.X) * progress);
                int previewY = (int)Math.Round(active.Y + (active.Y1 - active.Y) * progress);
                Point position = ToScreenCanvas(previewX, previewY);
                System.Windows.Controls.Image image = new()
                {
                    Source = source,
                    Width = source.PixelWidth * _screenScale,
                    Height = source.PixelHeight * _screenScale,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    Opacity = 0.95
                };
                previewCanvas.Children.Add(image);
                Canvas.SetLeft(image, position.X - canvasOrigin.X * _screenScale);
                Canvas.SetTop(image, position.Y - canvasOrigin.Y * _screenScale);
            }
        }

        private bool TryResolveVisual(string visualPath, int elapsed, out BitmapSource source) =>
            TryResolveVisual(_selectedScene?.Image, visualPath, elapsed, out source);

        private bool TryResolveVisual(string visualPath, int elapsed, out BitmapSource source, out Point canvasOrigin) =>
            TryResolveVisual(_selectedScene?.Image, visualPath, elapsed, out source, out canvasOrigin);

        private bool TryResolveVisual(WzImage defaultImage, string visualPath, int elapsed, out BitmapSource source)
            => TryResolveVisual(defaultImage, visualPath, elapsed, out source, out _);

        private bool TryResolveVisual(WzImage defaultImage, string visualPath, int elapsed, out BitmapSource source, out Point canvasOrigin)
        {
            try
            {
                string normalized = visualPath.Replace('\\', '/').Trim('/');
                WzImage image = defaultImage;
                string propertyPath = normalized;
                string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 1 && string.Equals(segments[0], "Effect", StringComparison.OrdinalIgnoreCase))
                {
                    image = Program.FindImage("Effect", segments[1]);
                    propertyPath = string.Join('/', segments.Skip(2));
                }
                else if (segments.Length > 1 && segments[0].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                {
                    image = Program.FindImage("Effect", segments[0]);
                    propertyPath = string.Join('/', segments.Skip(1));
                }
                WzImageProperty property = image?.GetFromPath(propertyPath);
                WzCanvasProperty canvas = SelectCanvas(property, elapsed);
                if (canvas == null)
                {
                    source = null;
                    canvasOrigin = default;
                    return false;
                }
                canvasOrigin = ResolveCanvasOrigin(canvas);
                if (_visualCache.TryGetValue(canvas, out source))
                    return source != null;
                using System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                source = SelectorDialogSupport.ToBitmapSource(bitmap);
                source?.Freeze();
                _visualCache[canvas] = source;
                return source != null;
            }
            catch
            {
                source = null;
                canvasOrigin = default;
                return false;
            }
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            HashSet<WzCanvasProperty> visited = new();
            WzCanvasProperty current = canvas;
            while (current != null && visited.Add(current))
            {
                if (current[WzCanvasProperty.OriginPropertyName] is WzVectorProperty origin)
                    return new Point(origin.X.Value, origin.Y.Value);

                WzImageProperty linked = current.GetLinkedWzImageProperty();
                current = linked as WzCanvasProperty;
            }
            return default;
        }

        private BitmapSource ResolveVisualPreview(string visualPath) => TryResolveVisual(visualPath, 0, out BitmapSource source) ? source : null;

        private static WzCanvasProperty SelectCanvas(WzImageProperty property, int elapsed)
        {
            if (property is WzCanvasProperty canvas)
                return canvas;
            if (property == null)
                return null;
            IReadOnlyList<WzImageProperty> children = SafeProperties(property);
            List<WzCanvasProperty> frames = children.OfType<WzCanvasProperty>()
                .OrderBy(item => ParseIndex(item.Name)).ToList();
            if (frames.Count > 0)
            {
                int totalDelay = frames.Sum(frame => Math.Max(1, InfoTool.GetInt(frame["delay"], 100)));
                int cursor = totalDelay == 0 ? 0 : elapsed % totalDelay;
                foreach (WzCanvasProperty frame in frames)
                {
                    int delay = Math.Max(1, InfoTool.GetInt(frame["delay"], 100));
                    if (cursor < delay)
                        return frame;
                    cursor -= delay;
                }
                return frames[^1];
            }
            return children.OrderBy(item => ParseIndex(item.Name))
                .Select(item => SelectCanvas(item, elapsed)).FirstOrDefault(item => item != null);
        }

        private static IReadOnlyList<WzImageProperty> SafeProperties(WzImageProperty property)
        {
            try
            {
                WzPropertyCollection properties = property?.WzProperties;
                return properties == null ? Array.Empty<WzImageProperty>() : properties.ToList();
            }
            catch
            {
                return Array.Empty<WzImageProperty>();
            }
        }

        private static bool SoundPathExists(string soundPath) => ResolveSoundPath(soundPath) != null;

        private static WzBinaryProperty ResolveSoundPath(string soundPath)
        {
            try
            {
                string normalized = soundPath.Replace('\\', '/').Trim('/');
                if (Program.AudioAssetCatalog is HaCreator.Audio.IAudioAssetCatalog catalog)
                {
                    if (catalog.Entries.Count == 0)
                        catalog.BuildIndexAsync().GetAwaiter().GetResult();
                    HaCreator.Audio.AudioAssetEntry catalogEntry = catalog.Find(normalized);
                    if (catalogEntry != null)
                        return catalog.LoadPropertyAsync(catalogEntry).GetAwaiter().GetResult();
                }
                string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string category = "Sound";
                int offset = 0;
                if (segments.Length > 0 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase))
                    offset = 1;
                else if (segments.Length > 0 && string.Equals(segments[0], "Effect", StringComparison.OrdinalIgnoreCase))
                {
                    category = "Effect";
                    offset = 1;
                }
                if (segments.Length <= offset)
                    return null;
                int imageEnd = Array.FindIndex(segments, offset, segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
                if (imageEnd < offset)
                    imageEnd = offset;
                string imageName = string.Join('/', segments.Skip(offset).Take(imageEnd - offset + 1));
                WzImage image = Program.FindImage(category, imageName)
                    ?? Program.FindImage(category, imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? imageName : imageName + ".img");
                if (image == null)
                    return null;
                string propertyPath = string.Join('/', segments.Skip(imageEnd + 1));
                return propertyPath.Length == 0
                    ? null
                    : image.GetFromPath(propertyPath)?.GetLinkedWzImageProperty() as WzBinaryProperty;
            }
            catch
            {
                return null;
            }
        }

        private (double Width, double Height) GetResolution()
        {
            string text = (resolutionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "800 × 600";
            string[] parts = text.Split('×');
            return parts.Length == 2 && double.TryParse(parts[0], out double width) && double.TryParse(parts[1], out double height)
                ? (width, height)
                : (800, 600);
        }

        private static string NextId(IEnumerable<string> ids)
        {
            HashSet<string> used = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; ; index++)
                if (!used.Contains(index.ToString()))
                    return index.ToString();
        }

        private string NextSceneName(WzObject parent)
        {
            HashSet<string> used = (_selectedSceneImage.Scenes ?? Array.Empty<CutsceneSceneModel>())
                .Where(scene => ReferenceEquals(scene.Parent, parent))
                .Select(scene => scene.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; ; index++)
            {
                string name = $"Scene{index}";
                if (!used.Contains(name) && !CutsceneRepository.HasProperty(parent, name))
                    return name;
            }
        }

        private string ValidateNewScenePath(string path)
        {
            return TryResolveNewScenePath(path, out _, out _, out _, out string error)
                ? null
                : error;
        }

        private bool TryResolveNewScenePath(
            string path,
            out WzObject parent,
            out string name,
            out string normalizedPath,
            out string error)
        {
            parent = null;
            name = null;
            normalizedPath = path?.Replace('\\', '/').Trim('/');
            error = null;

            WzImage image = _selectedSceneImage?.ResolveImage();
            if (image == null)
            {
                error = "Select a Direction image first.";
                return false;
            }

            string[] segments = normalizedPath?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (segments.Length < 2 || !string.Equals(segments[0], image.Name, StringComparison.OrdinalIgnoreCase))
            {
                error = $"The scene path must start with '{image.Name}/'.";
                return false;
            }

            name = segments[^1];
            if (name is "." or "..")
            {
                error = "The scene name is invalid.";
                return false;
            }

            WzObject current = image;
            for (int index = 1; index < segments.Length - 1; index++)
            {
                WzImageProperty child = current switch
                {
                    WzImage imageNode => imageNode.WzProperties.FirstOrDefault(property =>
                        property is WzSubProperty
                        && string.Equals(property.Name, segments[index], StringComparison.OrdinalIgnoreCase)),
                    WzSubProperty propertyNode => propertyNode.WzProperties.FirstOrDefault(property =>
                        property is WzSubProperty
                        && string.Equals(property.Name, segments[index], StringComparison.OrdinalIgnoreCase)),
                    _ => null
                };
                if (child is not WzSubProperty)
                {
                    error = $"The parent path '{string.Join('/', segments.Take(index + 1))}' does not exist.";
                    return false;
                }
                current = child;
            }

            parent = current;
            WzObject resolvedParent = parent;
            string resolvedName = name;
            bool alreadyUsed = (_selectedSceneImage.Scenes ?? Array.Empty<CutsceneSceneModel>())
                .Any(scene => ReferenceEquals(scene.Parent, resolvedParent)
                    && string.Equals(scene.Name, resolvedName, StringComparison.OrdinalIgnoreCase))
                || CutsceneRepository.HasProperty(resolvedParent, resolvedName);
            if (alreadyUsed)
            {
                error = $"A scene named '{name}' already exists at this path.";
                return false;
            }
            return true;
        }

        private void ClearHistory(CutsceneSceneModel scene)
        {
            if (scene == null)
                return;
            _undoHistory.Remove(scene);
            _redoHistory.Remove(scene);
            if (ReferenceEquals(_historySnapshot?.Scene, scene))
                _historySnapshot = null;
            if (ReferenceEquals(_historyTransactionStart?.Scene, scene))
                _historyTransactionStart = null;
            CommandManager.InvalidateRequerySuggested();
        }

        private static string EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static int ParseIndex(string value) => int.TryParse(value, out int index) ? index : int.MaxValue;
        private sealed record Choice(int Value, string Name);
        private sealed record TimelineTrack(string Name, Func<CutsceneEventModel, bool> Matches);

        private sealed record CutsceneHistoryEntry(
            CutsceneHistorySnapshot Before,
            CutsceneHistorySnapshot After);

        private sealed class CutsceneHistorySnapshot
        {
            public CutsceneHistorySnapshot(
                CutsceneSceneModel scene,
                IReadOnlyList<CutsceneEventHistorySnapshot> events,
                string selectedEventId)
            {
                Scene = scene;
                Events = events;
                SelectedEventId = selectedEventId;
            }

            public CutsceneSceneModel Scene { get; }
            public IReadOnlyList<CutsceneEventHistorySnapshot> Events { get; }
            public string SelectedEventId { get; }
        }

        private sealed class CutsceneEventHistorySnapshot
        {
            private CutsceneEventHistorySnapshot(
                string id,
                WzSubProperty source,
                int type,
                int start,
                int duration,
                int x,
                int y,
                int x1,
                int y1,
                int z,
                int field,
                string visual,
                string sound,
                string action,
                IReadOnlyList<CutsceneEquipmentHistorySnapshot> equipment,
                IReadOnlyList<CutsceneUnknownFieldHistorySnapshot> unknownFields)
            {
                Id = id;
                Source = source;
                Type = type;
                Start = start;
                Duration = duration;
                X = x;
                Y = y;
                X1 = x1;
                Y1 = y1;
                Z = z;
                Field = field;
                Visual = visual;
                Sound = sound;
                Action = action;
                Equipment = equipment;
                UnknownFields = unknownFields;
            }

            public string Id { get; }
            public WzSubProperty Source { get; }
            public int Type { get; }
            public int Start { get; }
            public int Duration { get; }
            public int X { get; }
            public int Y { get; }
            public int X1 { get; }
            public int Y1 { get; }
            public int Z { get; }
            public int Field { get; }
            public string Visual { get; }
            public string Sound { get; }
            public string Action { get; }
            public IReadOnlyList<CutsceneEquipmentHistorySnapshot> Equipment { get; }
            public IReadOnlyList<CutsceneUnknownFieldHistorySnapshot> UnknownFields { get; }

            public static CutsceneEventHistorySnapshot FromModel(CutsceneEventModel model)
            {
                return new CutsceneEventHistorySnapshot(
                    model.Id,
                    model.Source == null ? null : (WzSubProperty)model.Source.DeepClone(),
                    model.Type,
                    model.Start,
                    model.Duration,
                    model.X,
                    model.Y,
                    model.X1,
                    model.Y1,
                    model.Z,
                    model.Field,
                    model.Visual,
                    model.Sound,
                    model.Action,
                    model.Equipment
                        .Select(item => new CutsceneEquipmentHistorySnapshot(item.Slot, item.ItemId, item.SourceName))
                        .ToList(),
                    model.UnknownFields
                        .Select(field => new CutsceneUnknownFieldHistorySnapshot(
                            field.Name,
                            field.Value,
                            field.SourceProperty == null ? null : (WzImageProperty)field.SourceProperty.DeepClone(),
                            field.WrittenName))
                        .ToList());
            }

            public bool ContentEquals(CutsceneEventHistorySnapshot other)
            {
                if (other == null
                    || !string.Equals(Id, other.Id, StringComparison.Ordinal)
                    || Type != other.Type
                    || Start != other.Start
                    || Duration != other.Duration
                    || X != other.X
                    || Y != other.Y
                    || X1 != other.X1
                    || Y1 != other.Y1
                    || Z != other.Z
                    || Field != other.Field
                    || !string.Equals(Visual, other.Visual, StringComparison.Ordinal)
                    || !string.Equals(Sound, other.Sound, StringComparison.Ordinal)
                    || !string.Equals(Action, other.Action, StringComparison.Ordinal)
                    || Equipment.Count != other.Equipment.Count
                    || UnknownFields.Count != other.UnknownFields.Count)
                    return false;

                return Equipment.Zip(other.Equipment, (left, right) => left.Equals(right)).All(equal => equal)
                    && UnknownFields.Zip(other.UnknownFields, (left, right) => left.ContentEquals(right)).All(equal => equal);
            }

            public CutsceneEventModel CreateModel()
            {
                WzSubProperty source = Source == null
                    ? new WzSubProperty(Id)
                    : (WzSubProperty)Source.DeepClone();
                CutsceneEventModel result = CutsceneEventModel.FromProperty(source);
                ApplyTo(result);
                return result;
            }

            public void ApplyTo(CutsceneEventModel target)
            {
                target.Type = Type;
                target.Start = Start;
                target.Duration = Duration;
                target.X = X;
                target.Y = Y;
                target.X1 = X1;
                target.Y1 = Y1;
                target.Z = Z;
                target.Field = Field;
                target.Visual = Visual;
                target.Sound = Sound;
                target.Action = Action;

                target.Equipment.Clear();
                foreach (CutsceneEquipmentHistorySnapshot item in Equipment)
                    target.Equipment.Add(new CutsceneEquipmentEntry(item.Slot, item.ItemId, item.SourceName));

                target.UnknownFields.Clear();
                foreach (CutsceneUnknownFieldHistorySnapshot fieldSnapshot in UnknownFields)
                {
                    WzImageProperty sourceProperty = fieldSnapshot.SourceProperty == null
                        ? null
                        : (WzImageProperty)fieldSnapshot.SourceProperty.DeepClone();
                    CutsceneUnknownFieldEntry field = new(fieldSnapshot.Name, fieldSnapshot.Value, sourceProperty)
                    {
                        WrittenName = fieldSnapshot.WrittenName
                    };
                    target.UnknownFields.Add(field);
                }
            }
        }

        private sealed record CutsceneEquipmentHistorySnapshot(int Slot, int ItemId, string SourceName);

        private sealed class CutsceneUnknownFieldHistorySnapshot
        {
            public CutsceneUnknownFieldHistorySnapshot(
                string name,
                string value,
                WzImageProperty sourceProperty,
                string writtenName)
            {
                Name = name;
                Value = value;
                SourceProperty = sourceProperty;
                WrittenName = writtenName;
            }

            public string Name { get; }
            public string Value { get; }
            public WzImageProperty SourceProperty { get; }
            public string WrittenName { get; }

            public bool ContentEquals(CutsceneUnknownFieldHistorySnapshot other) => other != null
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal)
                && string.Equals(WrittenName, other.WrittenName, StringComparison.Ordinal);
        }

        private sealed class CharacterPreviewLayer : IDisposable
        {
            public CharacterPreviewLayer(DrawingBitmap bitmap, int x, int y, int zIndex)
            {
                Bitmap = bitmap;
                X = x;
                Y = y;
                ZIndex = zIndex;
            }

            public DrawingBitmap Bitmap { get; }
            public int X { get; }
            public int Y { get; }
            public int ZIndex { get; }
            public void Dispose() => Bitmap?.Dispose();
        }

        private sealed class CharacterPreviewImage
        {
            public CharacterPreviewImage(BitmapSource source, int anchorX, int anchorY)
            {
                Source = source;
                AnchorX = anchorX;
                AnchorY = anchorY;
            }

            public BitmapSource Source { get; }
            public int AnchorX { get; }
            public int AnchorY { get; }
        }
    }
}
