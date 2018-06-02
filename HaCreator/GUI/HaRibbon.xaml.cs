/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.CustomControls;
using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HaCreator.GUI
{
    /// <summary>
    /// Interaction logic for HaRibbon.xaml
    /// </summary>
    public partial class HaRibbon : UserControl
    {
        public HaRibbon()
        {
            InitializeComponent();
            this.PreviewMouseWheel += HaRibbon_PreviewMouseWheel;
        }

        void HaRibbon_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (ribbon.IsMouseOver)
            {
                HaList platformBox = (HaList)FindName("platformBox");
                if (platformBox.IsMouseOver)
                    platformBox.Scroll(e.Delta);
                else if (layerBox.IsMouseOver)
                    layerBox.Scroll(e.Delta);
            }
        }

        public int reducedHeight = 0;
        private int actualLayerIndex = 0;
        private int actualPlatform = 0;
        private int changingIndexCnt = 0;
        private List<Layer> layers = null;
        private bool hasMinimap = false;

        private void Ribbon_Loaded(object sender, RoutedEventArgs e)
        {
            Grid child = VisualTreeHelper.GetChild((DependencyObject)sender, 0) as Grid;
            if (child != null)
            {
                reducedHeight = (int)child.RowDefinitions[0].ActualHeight;
                child.RowDefinitions[0].Height = new GridLength(0);
            }
        }

        public static readonly RoutedUICommand New = new RoutedUICommand("New", "New", typeof(HaRibbon),
            new InputGestureCollection() { new KeyGesture(Key.N, ModifierKeys.Control) });
        public static readonly RoutedUICommand Open = new RoutedUICommand("Open", "Open", typeof(HaRibbon), 
            new InputGestureCollection() { new KeyGesture(Key.O, ModifierKeys.Control) });
        public static readonly RoutedUICommand Save = new RoutedUICommand("Save", "Save", typeof(HaRibbon),
            new InputGestureCollection() { new KeyGesture(Key.S, ModifierKeys.Control) });
        public static readonly RoutedUICommand Repack = new RoutedUICommand("Repack", "Repack", typeof(HaRibbon),
            new InputGestureCollection() {});
        public static readonly RoutedUICommand About = new RoutedUICommand("About", "About", typeof(HaRibbon),
            new InputGestureCollection() {});
        public static readonly RoutedUICommand Help = new RoutedUICommand("Help", "Help", typeof(HaRibbon),
            new InputGestureCollection() {});
        public static readonly RoutedUICommand Settings = new RoutedUICommand("Settings", "Settings", typeof(HaRibbon),
            new InputGestureCollection() {});
        public static readonly RoutedUICommand Exit = new RoutedUICommand("Exit", "Exit", typeof(HaRibbon),
            new InputGestureCollection() {});
        public static readonly RoutedUICommand ViewBoxes = new RoutedUICommand("ViewBoxes", "ViewBoxes", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Minimap = new RoutedUICommand("Minimap", "Minimap", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Parallax = new RoutedUICommand("Parallax", "Parallax", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Finalize = new RoutedUICommand("Finalize", "Finalize", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand AllLayerView = new RoutedUICommand("AllLayerView", "AllLayerView", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand MapSim = new RoutedUICommand("MapSim", "MapSim", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand RegenMinimap = new RoutedUICommand("RegenMinimap", "RegenMinimap", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Snapping = new RoutedUICommand("Snapping", "Snapping", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Random = new RoutedUICommand("Random", "Random", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand InfoMode = new RoutedUICommand("InfoMode", "InfoMode", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand HaRepacker = new RoutedUICommand("HaRepacker", "HaRepacker", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand LayerUp = new RoutedUICommand("LayerUp", "LayerUp", typeof(HaRibbon),
            new InputGestureCollection() { new KeyGesture(Key.OemPlus, ModifierKeys.Control) });
        public static readonly RoutedUICommand LayerDown = new RoutedUICommand("LayerDown", "LayerDown", typeof(HaRibbon),
            new InputGestureCollection() { new KeyGesture(Key.OemMinus, ModifierKeys.Control) });
        public static readonly RoutedUICommand AllPlatformView = new RoutedUICommand("AllPlatformView", "AllPlatformView", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand PlatformUp = new RoutedUICommand("PlatformUp", "PlatformUp", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand PlatformDown = new RoutedUICommand("PlatformDown", "PlatformDown", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand NewPlatform = new RoutedUICommand("NewPlatform", "NewPlatform", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand UserObjs = new RoutedUICommand("UserObjs", "UserObjs", typeof(HaRibbon),
            new InputGestureCollection() { });
        public static readonly RoutedUICommand Export = new RoutedUICommand("Export", "Export", typeof(HaRibbon),
            new InputGestureCollection() { });

        private void AlwaysExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void HasMinimap(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = hasMinimap;
        }

        private void New_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (NewClicked != null)
                NewClicked.Invoke();
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (OpenClicked != null)
                OpenClicked.Invoke();
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SaveClicked != null)
                SaveClicked.Invoke();
        }

        private void Repack_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (RepackClicked != null)
                RepackClicked.Invoke();
        }

        private void About_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (AboutClicked != null)
                AboutClicked.Invoke();
        }

        private void Help_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (HelpClicked != null)
                HelpClicked.Invoke();
        }

        private void Settings_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SettingsClicked != null)
                SettingsClicked.Invoke();
        }

        private void Exit_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ExitClicked != null)
                ExitClicked.Invoke();
        }

        private void ViewBoxes_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ViewToggled != null)
                ViewToggled.Invoke(tilesCheck.IsChecked, objsCheck.IsChecked, npcsCheck.IsChecked, mobsCheck.IsChecked, reactCheck.IsChecked, portalCheck.IsChecked, fhCheck.IsChecked, ropeCheck.IsChecked, chairCheck.IsChecked, tooltipCheck.IsChecked, bgCheck.IsChecked, miscCheck.IsChecked);
        }

        private void Minimap_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ShowMinimapToggled != null)
                ShowMinimapToggled.Invoke(((RibbonToggleButton)e.OriginalSource).IsChecked.Value);
        }

        private void Parallax_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ParallaxToggled != null)
                ParallaxToggled.Invoke(((RibbonToggleButton)e.OriginalSource).IsChecked.Value);
        }

        private void Finalize_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (FinalizeClicked != null)
                FinalizeClicked.Invoke();
        }

        private void MapSim_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (MapSimulationClicked != null)
                MapSimulationClicked.Invoke();
        }

        private void RegenMinimap_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (RegenerateMinimapClicked != null)
                RegenerateMinimapClicked.Invoke();
        }

        private void Snapping_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SnappingToggled != null)
                SnappingToggled.Invoke(((RibbonToggleButton)e.OriginalSource).IsChecked.Value);
        }

        private void Random_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (RandomTilesToggled != null)
                RandomTilesToggled.Invoke(((RibbonToggleButton)e.OriginalSource).IsChecked.Value);
        }

        private void InfoMode_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (InfoModeToggled != null)
                InfoModeToggled.Invoke(((RibbonToggleButton)e.OriginalSource).IsChecked.Value);
        }

        private void HaRepacker_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (HaRepackerClicked != null)
                HaRepackerClicked.Invoke();
        }

        private void Export_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ExportClicked != null)
                ExportClicked.Invoke();
        }

        #region Layer UI
        private void LayerUp_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (UserSettings.InverseUpDown && sender != null)
            {
                LayerDown_Executed(null, null);
            }
            else if (layerBox.IsEnabled && layerBox.SelectedIndex != (layerBox.Items.Count - 1))
            {
                layerBox.SelectedIndex++;
            }
        }

        private void LayerDown_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (UserSettings.InverseUpDown && sender != null)
            {
                LayerUp_Executed(null, null);
            }
            else if (layerBox.IsEnabled && layerBox.SelectedIndex != 0)
            {
                layerBox.SelectedIndex--;
            }
        }

        private void PlatformUp_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (UserSettings.InverseUpDown && sender != null)
            {
                PlatformDown_Executed(null, null);
            }
            else if (platformBox.IsEnabled && platformBox.SelectedIndex != (platformBox.Items.Count - 1))
            {
                platformBox.SelectedIndex++;
            }
        }

        private void PlatformDown_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (UserSettings.InverseUpDown && sender != null)
            {
                PlatformUp_Executed(null, null);
            }
            else if (platformBox.IsEnabled && platformBox.SelectedIndex != 0)
            {
                platformBox.SelectedIndex--;
            }
        }

        private void NewPlatform_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (NewPlatformClicked != null)
                NewPlatformClicked();
        }

        private void UserObjs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (UserObjsClicked != null)
                UserObjsClicked();
        }

        private void beginInternalEditing()
        {
            changingIndexCnt++;
        }

        private void endInternalEditing()
        {
            changingIndexCnt--;
        }

        private bool isInternal
        {
            get
            {
                return changingIndexCnt > 0;
            }
        }

        private void UpdateLocalLayerInfo()
        {
            actualLayerIndex = layerBox.SelectedIndex;
            actualPlatform = platformBox.SelectedItem == null ? 0 : (int)platformBox.SelectedItem;
        }

        private void UpdateRemoteLayerInfo()
        {
            if (LayerViewChanged != null)
                LayerViewChanged.Invoke(actualLayerIndex, actualPlatform, layerCheckbox.IsChecked.Value, (layerCheckbox.IsChecked.Value || platformCheckbox.IsChecked.Value));
        }

        private void AllLayerView_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            UpdateRemoteLayerInfo();
        }

        private void AllPlatformView_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            UpdateRemoteLayerInfo();
        }

        private void LoadPlatformsForLayer(SortedSet<int> zms)
        {
            beginInternalEditing();

            platformBox.ClearItems();
            foreach (int zm in zms)
            {
                platformBox.Items.Add(new HaListItem(zm.ToString(), zm));
            }
            platformBox.SelectedIndex = 0;

            endInternalEditing();
        }

        private void layerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInternal)
            {
                LoadPlatformsForLayer(layers[layerBox.SelectedIndex].zMList);
                UpdateLocalLayerInfo();
                UpdateRemoteLayerInfo();
            }
        }

        private void platformBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInternal)
            {
                UpdateLocalLayerInfo();
                UpdateRemoteLayerInfo();
            }
        }

        public void SetSelectedLayer(int layer, int platform, bool allLayers, bool allPlats)
        {
            // Disable callbacks
            beginInternalEditing();

            // Set layer info
            layerCheckbox.IsChecked = allLayers;
            layerBox.SelectedIndex = layer;
            LoadPlatformsForLayer(layers[layer].zMList);

            // Set platform info
            platformCheckbox.IsChecked = allPlats;
            platformBox.SelectedIndex = layers[layer].zMList.ToList().IndexOf(platform);
            actualPlatform = platform;

            // Update stuff
            UpdateLocalLayerInfo();

            // Re-enable callbacks
            endInternalEditing();
        }

        public void SetLayers(List<Layer> layers)
        {
            beginInternalEditing();

            this.layers = layers;
            layerBox.ClearItems();
            for (int i = 0; i < layers.Count; i++)
            {
                layerBox.Items.Add(new HaListItem(layers[i].ToString(), i));
            }

            endInternalEditing();
        }

        public void SetLayer(Layer layer)
        {
            beginInternalEditing();

            int oldIdx = layerBox.SelectedIndex;
            int i = layer.LayerNumber;
            layerBox.Items[i].Text = layer.ToString();
            layerBox.SelectedIndex = oldIdx;

            endInternalEditing();
        }

        #endregion

        public delegate void EmptyEvent();
        public delegate void ViewToggleEvent(bool? tiles, bool? objs, bool? npcs, bool? mobs, bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips, bool? backgrounds, bool? misc);
        public delegate void ToggleEvent(bool pressed);
        public delegate void LayerViewChangedEvent(int layer, int platform, bool allLayers, bool allPlats);

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
        public event EmptyEvent HaRepackerClicked;
        public event EmptyEvent ExportClicked;
        public event EmptyEvent NewPlatformClicked;
        public event EmptyEvent UserObjsClicked;
        public event EventHandler<System.Windows.Forms.KeyEventArgs> RibbonKeyDown;

        public void SetVisibilityCheckboxes(bool? tiles, bool? objs, bool? npcs, bool? mobs, bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips, bool? backgrounds, bool? misc)
        {
            tilesCheck.IsChecked = tiles;
            objsCheck.IsChecked = objs;
            npcsCheck.IsChecked = npcs;
            mobsCheck.IsChecked = mobs;
            reactCheck.IsChecked = reactors;
            portalCheck.IsChecked = portals;
            fhCheck.IsChecked = footholds;
            ropeCheck.IsChecked = ropes;
            chairCheck.IsChecked = chairs;
            tooltipCheck.IsChecked = tooltips;
            bgCheck.IsChecked = backgrounds;
            miscCheck.IsChecked = misc;
        }

        public void SetEnabled(bool enabled)
        {
            viewTab.IsEnabled = enabled;
            toolsTab.IsEnabled = enabled;
            statTab.IsEnabled = enabled;
            saveBtn.IsEnabled = enabled;
            exportBtn.IsEnabled = enabled;
            //resetLayerBoxIfNeeded();
        }

        public void SetOptions(bool minimap, bool parallax, bool snap, bool random, bool infomode)
        {
            minimapBtn.IsChecked = minimap;
            parallaxBtn.IsChecked = parallax;
            snapBtn.IsChecked = snap;
            randomBtn.IsChecked = random;
            infomodeBtn.IsChecked = infomode;
        }

        public void SetMousePos(int virtualX, int virtualY, int physicalX, int physicalY)
        {
            this.virtualPos.Text = "X: " + virtualX.ToString() + "\nY: " + virtualY.ToString();
            this.physicalPos.Text = "X: " + physicalX.ToString() + "\nY: " + physicalY.ToString();
        }

        public void SetItemDesc(string desc)
        {
            itemDesc.Text = desc;
        }

        public void SetHasMinimap(bool hasMinimap)
        {
            this.hasMinimap = hasMinimap;
            CommandManager.InvalidateRequerySuggested();
        }
        
        private void ChangeAllCheckboxes(bool? state)
        {
            foreach (CheckBox cb in new CheckBox[] { tilesCheck, objsCheck, npcsCheck, mobsCheck, reactCheck, portalCheck, fhCheck, ropeCheck, chairCheck, tooltipCheck, bgCheck, miscCheck })
            {
                cb.IsChecked = state;
            }
            ViewBoxes_Executed(null, null);
        }

        private void allFullCheck_Click(object sender, RoutedEventArgs e)
        {
            ChangeAllCheckboxes(true);
        }

        private void allHalfCheck_Click(object sender, RoutedEventArgs e)
        {
            ChangeAllCheckboxes(null);
        }

        private void allClearCheck_Click(object sender, RoutedEventArgs e)
        {
            ChangeAllCheckboxes(false);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key != Key.Down && e.Key != Key.Up && RibbonKeyDown != null)
            {
                RibbonKeyDown.Invoke(this, new System.Windows.Forms.KeyEventArgs((System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key)));
            }
        }
    }
}