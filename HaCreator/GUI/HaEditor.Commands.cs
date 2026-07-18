using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI
{
    public partial class HaEditor
    {
        private bool _hasMinimap;
        private bool _minimapEnabled;
        private bool _parallaxEnabled;
        private bool _snappingEnabled;
        private bool _randomTilesEnabled;
        private bool _infoModeEnabled;
        private bool _animateMapObjectPreviews;

        public static readonly RoutedUICommand New = CreateCommand(nameof(New), new KeyGesture(Key.N, ModifierKeys.Control));
        public static readonly RoutedUICommand Open = CreateCommand(nameof(Open), new KeyGesture(Key.O, ModifierKeys.Control));
        public static readonly RoutedUICommand Export = CreateCommand(nameof(Export), new KeyGesture(Key.S, ModifierKeys.Control));
        public static readonly RoutedUICommand Save = CreateCommand(nameof(Save));
        public static readonly RoutedUICommand Repack = CreateCommand(nameof(Repack));
        public static readonly RoutedUICommand About = CreateCommand(nameof(About));
        public static readonly RoutedUICommand Help = CreateCommand(nameof(Help));
        public static readonly RoutedUICommand Settings = CreateCommand(nameof(Settings));
        public static readonly RoutedUICommand Exit = CreateCommand(nameof(Exit));
        public static readonly RoutedUICommand Minimap = CreateCommand(nameof(Minimap));
        public static readonly RoutedUICommand Parallax = CreateCommand(nameof(Parallax));
        public static readonly RoutedUICommand Finalize = CreateCommand(nameof(Finalize));
        public static readonly RoutedUICommand MapSim = CreateCommand(nameof(MapSim));
        public static readonly RoutedUICommand RegenMinimap = CreateCommand(nameof(RegenMinimap));
        public static readonly RoutedUICommand Snapping = CreateCommand(nameof(Snapping));
        public static readonly RoutedUICommand Random = CreateCommand(nameof(Random));
        public static readonly RoutedUICommand InfoMode = CreateCommand(nameof(InfoMode));
        public static readonly RoutedUICommand AnimateMapObjectPreviews = CreateCommand(nameof(AnimateMapObjectPreviews));
        public static readonly RoutedUICommand HaRepacker = CreateCommand(nameof(HaRepacker));
        public static readonly RoutedUICommand NewPlatform = CreateCommand(nameof(NewPlatform));
        public static readonly RoutedUICommand UserObjs = CreateCommand(nameof(UserObjs));
        public static readonly RoutedUICommand PhysicsEdit = CreateCommand(nameof(PhysicsEdit));
        public static readonly RoutedUICommand ShowQuestEditorWindow = CreateCommand(nameof(ShowQuestEditorWindow));
        public static readonly RoutedUICommand ShowMapProperties = CreateCommand(nameof(ShowMapProperties));

        private static RoutedUICommand CreateCommand(string name, KeyGesture gesture = null)
        {
            var gestures = new InputGestureCollection();
            if (gesture != null)
                gestures.Add(gesture);
            return new RoutedUICommand(name, name, typeof(HaEditor), gestures);
        }

        private void AlwaysExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
        private void HasMinimap(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _hasMinimap;

        private void New_Executed(object sender, ExecutedRoutedEventArgs e) => NewClicked?.Invoke();
        private void Open_Executed(object sender, ExecutedRoutedEventArgs e) => OpenClicked?.Invoke();
        private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => SaveClicked?.Invoke();
        private void Repack_Executed(object sender, ExecutedRoutedEventArgs e) => RepackClicked?.Invoke();
        private void About_Executed(object sender, ExecutedRoutedEventArgs e) => AboutClicked?.Invoke();
        private void Help_Executed(object sender, ExecutedRoutedEventArgs e) => HelpClicked?.Invoke();
        private void Settings_Executed(object sender, ExecutedRoutedEventArgs e) => SettingsClicked?.Invoke();
        private void Exit_Executed(object sender, ExecutedRoutedEventArgs e) => ExitClicked?.Invoke();
        private void Finalize_Executed(object sender, ExecutedRoutedEventArgs e) => FinalizeClicked?.Invoke();
        private void MapSim_Executed(object sender, ExecutedRoutedEventArgs e) => MapSimulationClicked?.Invoke();
        private void RegenMinimap_Executed(object sender, ExecutedRoutedEventArgs e) => RegenerateMinimapClicked?.Invoke();
        private void HaRepacker_Executed(object sender, ExecutedRoutedEventArgs e) => HaRepackerClicked?.Invoke();
        private void Export_Executed(object sender, ExecutedRoutedEventArgs e) => ExportClicked?.Invoke();
        private void NewPlatform_Executed(object sender, ExecutedRoutedEventArgs e) => NewPlatformClicked?.Invoke();
        private void UserObjs_Executed(object sender, ExecutedRoutedEventArgs e) => UserObjsClicked?.Invoke();
        private void PhysicsEdit_Executed(object sender, ExecutedRoutedEventArgs e) => MapPhysicsClicked?.Invoke();
        private void ShowQuestEditorWindow_Executed(object sender, ExecutedRoutedEventArgs e) => ShowQuestEditorWindowClicked?.Invoke();
        private void ShowMapProperties_Executed(object sender, ExecutedRoutedEventArgs e) => ShowMapPropertiesClicked?.Invoke();

        private void Minimap_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _minimapEnabled = !_minimapEnabled;
            ShowMinimapToggled?.Invoke(_minimapEnabled);
        }

        private void Parallax_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _parallaxEnabled = !_parallaxEnabled;
            ParallaxToggled?.Invoke(_parallaxEnabled);
        }

        private void Snapping_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SetSnappingFromShell(!_snappingEnabled);
        }

        private void InfoMode_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _infoModeEnabled = !_infoModeEnabled;
            InfoModeToggled?.Invoke(_infoModeEnabled);
        }

        private void Random_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _randomTilesEnabled = !_randomTilesEnabled;
            RandomTilesToggled?.Invoke(_randomTilesEnabled);
        }

        private void AnimateMapObjectPreviews_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _animateMapObjectPreviews = !_animateMapObjectPreviews;
            MapObjectPreviewAnimationToggled?.Invoke(_animateMapObjectPreviews);
        }

        public void SelectLayerFromShell(int layer)
        {
            Board board = multiBoard?.SelectedBoard;
            if (board == null || layer < 0 || layer >= board.Layers.Count)
                return;

            int platform = board.Layers[layer].zMList.FirstOrDefault();
            RaiseLayerViewChanged(layer, platform, board.Layers[layer].tS);
        }

        public void SelectPlatformFromShell(int platform)
        {
            Board board = multiBoard?.SelectedBoard;
            if (board == null || board.SelectedLayerIndex < 0)
                return;

            RaiseLayerViewChanged(board.SelectedLayerIndex, platform, null);
        }

        public void SetAllLayersFromShell(bool showAll)
        {
            Board board = multiBoard?.SelectedBoard;
            if (board == null)
                return;

            LayerViewChanged?.Invoke(board.SelectedLayerIndex, board.SelectedPlatform, showAll,
                showAll || inspectorAllPlatformsCheckBox.IsChecked == true, null);
        }

        public void SetAllPlatformsFromShell(bool showAll)
        {
            Board board = multiBoard?.SelectedBoard;
            if (board == null)
                return;

            LayerViewChanged?.Invoke(board.SelectedLayerIndex, board.SelectedPlatform,
                inspectorAllLayersCheckBox.IsChecked == true,
                inspectorAllLayersCheckBox.IsChecked == true || showAll, null);
        }

        private void RaiseLayerViewChanged(int layer, int platform, string tileSet)
        {
            bool allLayers = inspectorAllLayersCheckBox.IsChecked == true;
            bool allPlatforms = allLayers || inspectorAllPlatformsCheckBox.IsChecked == true;
            LayerViewChanged?.Invoke(layer, platform, allLayers, allPlatforms, tileSet);
        }

        public void SetSnappingFromShell(bool enabled)
        {
            _snappingEnabled = enabled;
            SnappingToggled?.Invoke(enabled);
        }

        public void SetLayers(ReadOnlyCollection<Layer> layers) => RefreshInspectorLayerControls();
        public void SetLayer(Layer layer) => RefreshInspectorLayerControls();

        public void SetSelectedLayer(int layer, int platform, bool allLayers, bool allPlatforms)
        {
            RefreshInspectorLayerControls();
        }

        public void SetVisibilityCheckboxes(bool? tiles, bool? objects, bool? npcs, bool? mobs,
            bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips,
            bool? backgrounds, bool? misc, bool? mirrorField)
        {
            _syncingVisibilityControls = true;
            bool?[] states = { tiles, objects, npcs, mobs, reactors, portals, footholds, ropes, chairs, tooltips, backgrounds, misc, mirrorField };
            CheckBox[] checkBoxes = visibilityFiltersPanel.Children.OfType<CheckBox>().ToArray();
            for (int index = 0; index < Math.Min(states.Length, checkBoxes.Length); index++)
                checkBoxes[index].IsChecked = states[index];
            _syncingVisibilityControls = false;
        }

        public bool? GetVisibilityFromShell(string itemType) => GetVisibilityCheckBox(itemType)?.IsChecked;

        public void SetVisibilityFromShell(string itemType, bool? state)
        {
            CheckBox checkBox = GetVisibilityCheckBox(itemType);
            if (checkBox == null)
                return;

            _syncingVisibilityControls = true;
            checkBox.IsChecked = state;
            _syncingVisibilityControls = false;
            RaiseVisibilityChanged();
        }

        public void SetAllVisibilityFromShell(bool? state)
        {
            _syncingVisibilityControls = true;
            foreach (CheckBox checkBox in visibilityFiltersPanel.Children.OfType<CheckBox>())
                checkBox.IsChecked = state;
            _syncingVisibilityControls = false;
            RaiseVisibilityChanged();
        }

        private CheckBox GetVisibilityCheckBox(string itemType)
        {
            return visibilityFiltersPanel.Children.OfType<CheckBox>()
                .FirstOrDefault(checkBox => string.Equals(checkBox.Tag as string, itemType, StringComparison.Ordinal));
        }

        private void RaiseVisibilityChanged()
        {
            bool? State(string tag) => GetVisibilityCheckBox(tag)?.IsChecked;
            ViewToggled?.Invoke(State("Tiles"), State("Objects"), State("NPCs"), State("Mobs"),
                State("Reactors"), State("Portals"), State("Footholds"), State("Ropes"),
                State("Chairs"), State("Tooltips"), State("Backgrounds"), State("Other"), State("Mirror field"));
        }

        public void SetEnabled(bool enabled)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void SetOptions(bool minimap, bool parallax, bool snapping, bool random, bool infoMode)
        {
            _minimapEnabled = minimap;
            _parallaxEnabled = parallax;
            _snappingEnabled = snapping;
            _randomTilesEnabled = random;
            _infoModeEnabled = infoMode;
            _animateMapObjectPreviews = ApplicationSettings.AnimateMapObjectPreviews;
            RefreshInspectorLayerControls();
        }

        public void SetHasMinimap(bool hasMinimap)
        {
            _hasMinimap = hasMinimap;
            CommandManager.InvalidateRequerySuggested();
        }

        public delegate void EmptyEvent();
        public delegate void ViewToggleEvent(bool? tiles, bool? objects, bool? npcs, bool? mobs, bool? reactors,
            bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField);
        public delegate void ToggleEvent(bool pressed);
        public delegate void LayerViewChangedEvent(int layer, int platform, bool allLayers, bool allPlatforms, string tileSet);

        public event EmptyEvent NewClicked;
        public event EmptyEvent OpenClicked;
        public event EmptyEvent SaveClicked;
        public event EmptyEvent RepackClicked;
        public event EmptyEvent AboutClicked;
        public event EmptyEvent HelpClicked;
        public event EmptyEvent SettingsClicked;
        public event EmptyEvent ExitClicked;
        public event EmptyEvent FinalizeClicked;
        public event ViewToggleEvent ViewToggled;
        public event ToggleEvent ShowMinimapToggled;
        public event ToggleEvent ParallaxToggled;
        public event LayerViewChangedEvent LayerViewChanged;
        public event EmptyEvent MapSimulationClicked;
        public event EmptyEvent RegenerateMinimapClicked;
        public event ToggleEvent SnappingToggled;
        public event ToggleEvent RandomTilesToggled;
        public event ToggleEvent InfoModeToggled;
        public event ToggleEvent MapObjectPreviewAnimationToggled;
        public event EmptyEvent HaRepackerClicked;
        public event EmptyEvent ExportClicked;
        public event EmptyEvent NewPlatformClicked;
        public event EmptyEvent UserObjsClicked;
        public event EmptyEvent MapPhysicsClicked;
        public event EmptyEvent ShowQuestEditorWindowClicked;
        public event EmptyEvent ShowMapPropertiesClicked;
    }
}
