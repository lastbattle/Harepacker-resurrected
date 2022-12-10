/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.CustomControls;
using HaCreator.GUI.EditorPanels;
using HaCreator.GUI;
using HaCreator.GUI.InstanceEditor;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MapleLib.WzLib;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Exceptions;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;

using SystemWinCtl = System.Windows.Controls;
using HaSharedLibrary;

namespace HaCreator.MapEditor
{
    public class HaCreatorStateManager
    {
        private readonly MultiBoard multiBoard;
        private readonly HaRibbon ribbon;
        private readonly System.Windows.Controls.TabControl tabs;

        // StatusBar (bottom)
        private readonly SystemWinCtl.TextBlock textblock_CursorX;
        private readonly SystemWinCtl.TextBlock textblock_CursorY;
        private readonly SystemWinCtl.TextBlock textblock_RCursorX;
        private readonly SystemWinCtl.TextBlock textblock_RCursorY;
        private readonly SystemWinCtl.TextBlock textblock_selectedItem;

        private readonly InputHandler input;
        private TilePanel tilePanel;
        private ObjPanel objPanel;
        private System.Windows.Controls.ScrollViewer editorPanel;
        public readonly BackupManager backupMan;

        public HaCreatorStateManager(MultiBoard multiBoard, HaRibbon ribbon, System.Windows.Controls.TabControl tabs, InputHandler input, System.Windows.Controls.ScrollViewer editorPanel,
            SystemWinCtl.TextBlock textblock_CursorX, SystemWinCtl.TextBlock textblock_CursorY, SystemWinCtl.TextBlock textblock_RCursorX, SystemWinCtl.TextBlock textblock_RCursorY, SystemWinCtl.TextBlock textblock_selectedItem)
        {
            this.multiBoard = multiBoard;
            multiBoard.HaCreatorStateManager = this;

            this.ribbon = ribbon;
            this.tabs = tabs;
            this.input = input;
            this.editorPanel = editorPanel;

            // Status bar
            this.textblock_CursorX = textblock_CursorX;
            this.textblock_CursorY = textblock_CursorY;
            this.textblock_RCursorX = textblock_RCursorX;
            this.textblock_RCursorY = textblock_RCursorY;
            this.textblock_selectedItem = textblock_selectedItem;

            this.backupMan = new BackupManager(multiBoard, input, this, tabs);

            this.ribbon.NewClicked += Ribbon_NewClicked;
            this.ribbon.OpenClicked += Ribbon_OpenClicked;
            this.ribbon.SaveClicked += Ribbon_SaveClicked;
            this.ribbon.RepackClicked += Ribbon_RepackClicked;
            this.ribbon.AboutClicked += Ribbon_AboutClicked;
            this.ribbon.HelpClicked += Ribbon_HelpClicked;
            this.ribbon.SettingsClicked += Ribbon_SettingsClicked;
            this.ribbon.ExitClicked += Ribbon_ExitClicked;
            this.ribbon.ViewToggled += Ribbon_ViewToggled;
            this.ribbon.ShowMinimapToggled += Ribbon_ShowMinimapToggled;
            this.ribbon.ParallaxToggled += Ribbon_ParallaxToggled;
            this.ribbon.LayerViewChanged += ribbon_LayerViewChanged;
            this.ribbon.MapSimulationClicked += Ribbon_MapSimulationClicked;
            this.ribbon.RegenerateMinimapClicked += Ribbon_RegenerateMinimapClicked;
            this.ribbon.SnappingToggled += Ribbon_SnappingToggled;
            this.ribbon.RandomTilesToggled += Ribbon_RandomTilesToggled;
            this.ribbon.InfoModeToggled += Ribbon_InfoModeToggled;
            this.ribbon.HaRepackerClicked += Ribbon_HaRepackerClicked;
            this.ribbon.FinalizeClicked += Ribbon_FinalizeClicked;
            this.ribbon.NewPlatformClicked += ribbon_NewPlatformClicked;
            this.ribbon.UserObjsClicked += Ribbon_UserObjsClicked;
            this.ribbon.ExportClicked += Ribbon_ExportClicked;
            this.ribbon.RibbonKeyDown += multiBoard.DxContainer_KeyDown;
            this.ribbon.MapPhysicsClicked += Ribbon_EditMapPhysicsClicked;

            // Debug
            this.ribbon.ShowMapPropertiesClicked += Ribbon_ShowMapPropertiesClicked;
            //

            this.tabs.SelectionChanged += Tabs_SelectionChanged;

            this.multiBoard.OnBringToFrontClicked += MultiBoard_OnBringToFrontClicked;
            this.multiBoard.OnEditBaseClicked += MultiBoard_OnEditBaseClicked;
            this.multiBoard.OnEditInstanceClicked += MultiBoard_OnEditInstanceClicked;
            this.multiBoard.OnLayerTSChanged += MultiBoard_OnLayerTSChanged;
            this.multiBoard.OnSendToBackClicked += MultiBoard_OnSendToBackClicked;
            this.multiBoard.ReturnToSelectionState += MultiBoard_ReturnToSelectionState;
            this.multiBoard.SelectedItemChanged += MultiBoard_SelectedItemChanged;
            this.multiBoard.MouseMoved += MultiBoard_MouseMoved;
            this.multiBoard.ImageDropped += MultiBoard_ImageDropped;
            this.multiBoard.ExportRequested += Ribbon_ExportClicked;
            this.multiBoard.LoadRequested += Ribbon_OpenClicked;
            this.multiBoard.CloseTabRequested += MultiBoard_CloseTabRequested;
            this.multiBoard.SwitchTabRequested += MultiBoard_SwitchTabRequested;
            this.multiBoard.BackupCheck += MultiBoard_BackupCheck;
            this.multiBoard.BoardRemoved += MultiBoard_BoardRemoved;
            this.multiBoard.MinimapStateChanged += MultiBoard_MinimapStateChanged;

            multiBoard.Visibility = System.Windows.Visibility.Collapsed;
            ribbon.SetEnabled(false);
        }

        public static int PositiveMod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        void MultiBoard_SwitchTabRequested(object sender, bool reverse)
        {
            tabs.SelectedItem = tabs.Items[PositiveMod(tabs.Items.IndexOf(tabs.SelectedItem) + (reverse ? -1 : 1), tabs.Items.Count)];
        }

        void MultiBoard_CloseTabRequested()
        {
            tabs.Items.Remove(tabs.SelectedItem);
        }

        #region MultiBoard Events
        void MultiBoard_MinimapStateChanged(object sender, bool hasMm)
        {
            ribbon.SetHasMinimap(hasMm);
        }

        void MultiBoard_BoardRemoved(object sender, EventArgs e)
        {
            Board board = (Board)sender;
            backupMan.DeleteBackup(board.UniqueID);
        }

        void MultiBoard_BackupCheck()
        {
            try
            {
                backupMan.BackupCheck();
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Backup failed! Error:{0}\r\n{1}", e.Message, e.StackTrace));
            }
        }

        void MultiBoard_ImageDropped(Board selectedBoard, System.Drawing.Bitmap bmp, string name, Microsoft.Xna.Framework.Point pos)
        {
            WaitWindow ww = new WaitWindow("Processing \"" + name + "\"...");
            ww.Show();
            Application.DoEvents();
            ObjectInfo oi = null;
            try
            {
                oi = multiBoard.UserObjects.Add(bmp, name);
            }
            catch (NameAlreadyUsedException)
            {
                MessageBox.Show("\"" + name + "\" could not be added because an object with the same name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                ww.EndWait();
            }
            selectedBoard.BoardItems.Add(oi.CreateInstance(selectedBoard.SelectedLayer, selectedBoard, pos.X, pos.Y, 0, false), true);
            objPanel.OnL1Changed(UserObjectsManager.l1);
        }

        /// <summary>
        /// Mouse move event
        /// </summary>
        /// <param name="selectedBoard"></param>
        /// <param name="oldPos"></param>
        /// <param name="newPos"></param>
        /// <param name="currPhysicalPos"></param>
        void MultiBoard_MouseMoved(Board selectedBoard, Microsoft.Xna.Framework.Point oldPos, Microsoft.Xna.Framework.Point newPos, Microsoft.Xna.Framework.Point currPhysicalPos)
        {
            textblock_CursorX.Text = currPhysicalPos.X.ToString();
            textblock_CursorY.Text = currPhysicalPos.Y.ToString();

            textblock_RCursorX.Text = newPos.X.ToString();
            textblock_RCursorY.Text = newPos.Y.ToString();
        }

        /// <summary>
        /// Selected item event
        /// </summary>
        /// <param name="selectedItem"></param>
        void MultiBoard_SelectedItemChanged(BoardItem selectedItem)
        {
            if (selectedItem != null)
            {
                textblock_selectedItem.Text = (CreateItemDescription(selectedItem).Replace(Environment.NewLine, " - "));
            }
            else
            {
                textblock_selectedItem.Text = string.Empty;
            }
        }

        void MultiBoard_ReturnToSelectionState()
        {
            // No need to lock because SelectionMode() and ExitEditMode() are both thread-safe
            if (multiBoard.SelectedBoard == null)
                return;

            multiBoard.SelectedBoard.Mouse.SelectionMode();
            ExitEditMode();
            multiBoard.Focus();
        }

        void MultiBoard_OnSendToBackClicked(BoardItem boardRefItem)
        {
            lock (multiBoard)
            {
                foreach (BoardItem item in boardRefItem.Board.SelectedItems)
                {
                    if (item.Z > 0)
                    {
                        item.Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, item.Z, 0) });
                        item.Z = 0;
                    }
                }
                boardRefItem.Board.BoardItems.Sort();
            }
            multiBoard.Focus();
        }

        void MultiBoard_OnLayerTSChanged(Layer layer)
        {
            ribbon.SetLayer(layer);
        }

        void MultiBoard_OnEditInstanceClicked(BoardItem item)
        {
            InputHandler.ClearBoundItems(multiBoard.SelectedBoard);
            try
            {
                if (item is ObjectInstance)
                {
                    new ObjectInstanceEditor((ObjectInstance)item).ShowDialog();
                }
                else if (item is TileInstance)
                {
                    new TileInstanceEditor((TileInstance)item).ShowDialog();
                }
                else if (item is Chair)
                {
                    new GeneralInstanceEditor(item).ShowDialog();
                }
                else if (item is FootholdAnchor)
                {
                    FootholdLine[] selectedFootholds = FootholdLine.GetSelectedFootholds(item.Board);
                    if (selectedFootholds.Length > 0)
                    {
                        new FootholdEditor(selectedFootholds).ShowDialog();
                    }
                    else
                    {
                        new GeneralInstanceEditor(item).ShowDialog();
                    }
                }
                else if (item is RopeAnchor ropeItem)
                {
                    new RopeInstanceEditor(ropeItem).ShowDialog();
                }
                else if (item is LifeInstance lifeItem)
                {
                    new LifeInstanceEditor(lifeItem).ShowDialog();
                }
                else if (item is ReactorInstance reactorItem)
                {
                    new ReactorInstanceEditor(reactorItem).ShowDialog();
                }
                else if (item is BackgroundInstance backgroundItem)
                {
                    new BackgroundInstanceEditor(backgroundItem).ShowDialog();
                }
                else if (item is PortalInstance portal)
                {
                    new PortalInstanceEditor(portal).ShowDialog();
                }
                else if (item is ToolTipInstance tooltipItem)
                {
                    new TooltipInstanceEditor(tooltipItem).ShowDialog();
                } 
                else if (item is MirrorFieldData mirrorFieldItem)
                {
                    new MirrorFieldEditor(mirrorFieldItem).ShowDialog();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("An error occurred while presenting the instance editor for {0}:\r\n{1}", item.GetType().Name, e.ToString()));
            }
        }

        void MultiBoard_OnEditBaseClicked(BoardItem item)
        {
            //TODO
        }

        void MultiBoard_OnBringToFrontClicked(BoardItem boardRefItem)
        {
            lock (multiBoard)
            {
                foreach (BoardItem item in boardRefItem.Board.SelectedItems)
                {
                    int oldZ = item.Z;
                    if (item is BackgroundInstance)
                    {
                        IList list = ((BackgroundInstance)item).front ? multiBoard.SelectedBoard.BoardItems.FrontBackgrounds : multiBoard.SelectedBoard.BoardItems.BackBackgrounds;
                        int highestZ = 0;
                        foreach (BackgroundInstance bg in list)
                            if (bg.Z > highestZ)
                                highestZ = bg.Z;
                        item.Z = highestZ + 1;
                    }
                    else
                    {
                        int highestZ = 0;
                        foreach (LayeredItem layeredItem in multiBoard.SelectedBoard.BoardItems.TileObjs)
                            if (layeredItem.Z > highestZ) highestZ = layeredItem.Z;
                        item.Z = highestZ + 1;
                    }
                    if (item.Z != oldZ)
                        item.Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, oldZ, item.Z) });
                }
            }
            boardRefItem.Board.BoardItems.Sort();
        }
        #endregion

        #region Tab Events
        /// <summary>
        /// Context menu for editing map info (right clicking)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapEditInfo(object sender, EventArgs e)
        {
            System.Windows.Controls.MenuItem item = (System.Windows.Controls.MenuItem)sender;
            if (item == null)
                return;

            System.Windows.Controls.TabItem tabItem = (System.Windows.Controls.TabItem)item.Tag;
            TabItemContainer container = (TabItemContainer)tabItem.Tag;

            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                InfoEditor infoEditor = new InfoEditor(selectedBoard, selectedBoard.MapInfo, multiBoard, tabItem);
                infoEditor.ShowDialog();
                if (selectedBoard.ParentControl.SelectedBoard == selectedBoard)
                    selectedBoard.ParentControl.AdjustScrollBars();
            }
        }

        /// <summary>
        /// Context menu for adding map VR
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapAddVR(object sender, EventArgs e)
        {
            System.Windows.Controls.MenuItem item = (System.Windows.Controls.MenuItem)sender;
            if (item == null)
                return;

            System.Windows.Controls.TabItem tabItem = (System.Windows.Controls.TabItem)item.Tag;
            TabItemContainer container = (TabItemContainer)tabItem.Tag;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                if (selectedBoard.MapInfo.Image != null)
                {
                    Microsoft.Xna.Framework.Rectangle VR;
                    Microsoft.Xna.Framework.Point mapCenter, mapSize, minimapCenter, minimapSize;
                    bool hasVR, hasMinimap;
                    MapLoader.GetMapDimensions(selectedBoard.MapInfo.Image, out VR, out mapCenter, out mapSize, out minimapCenter, out minimapSize, out hasVR, out hasMinimap);
                    selectedBoard.VRRectangle = new VRRectangle(selectedBoard, VR);
                }
                else
                {
                    selectedBoard.VRRectangle = new VRRectangle(selectedBoard, new Microsoft.Xna.Framework.Rectangle(-selectedBoard.CenterPoint.X + 100, -selectedBoard.CenterPoint.Y + 100, selectedBoard.MapSize.X - 200, selectedBoard.MapSize.Y - 200));
                }
            }
        }

        /// <summary>
        /// Context menu for adding mini map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapAddMinimap(object sender, EventArgs e)
        {
            System.Windows.Controls.MenuItem item = (System.Windows.Controls.MenuItem)sender;
            if (item == null)
                return;

            System.Windows.Controls.TabItem tabItem = (System.Windows.Controls.TabItem)item.Tag;
            TabItemContainer container = (TabItemContainer)tabItem.Tag;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                if (selectedBoard.MapInfo.Image != null)
                {
                    Microsoft.Xna.Framework.Rectangle VR;
                    Microsoft.Xna.Framework.Point mapCenter, mapSize, minimapCenter, minimapSize;
                    bool hasVR, hasMinimap;
                    MapLoader.GetMapDimensions(selectedBoard.MapInfo.Image, out VR, out mapCenter, out mapSize, out minimapCenter, out minimapSize, out hasVR, out hasMinimap);
                    selectedBoard.MinimapRectangle = new MinimapRectangle(selectedBoard, new Microsoft.Xna.Framework.Rectangle(-minimapCenter.X, -minimapCenter.Y, minimapSize.X, minimapSize.Y));
                }
                else
                {
                    selectedBoard.MinimapRectangle = new MinimapRectangle(selectedBoard, new Microsoft.Xna.Framework.Rectangle(-selectedBoard.CenterPoint.X + 100, -selectedBoard.CenterPoint.Y + 100, selectedBoard.MapSize.X - 200, selectedBoard.MapSize.Y - 200));
                }
                selectedBoard.RegenerateMinimap();
            }
        }

        /// <summary>
        /// Context menu for closing of the map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseMapTab(object sender, EventArgs e)
        {
            if (tabs.Items.Count <= 0) // at least 1 tabs for now
            {
                return;
            }
            if (MessageBox.Show("Are you sure you want to close this map?", "Close", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            System.Windows.Controls.MenuItem item = (System.Windows.Controls.MenuItem)sender;
            if (item == null)
                return;

            System.Windows.Controls.TabItem tabItem = (System.Windows.Controls.TabItem)item.Tag;
            TabItemContainer container = (TabItemContainer)tabItem.Tag;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                tabs.SelectedItem = tabs.Items[0];
                tabs.Items.Remove(tabItem);

                selectedBoard.Dispose();
            }

            UpdateEditorPanelVisibility();
        }

        /// <summary>
        /// If there's no more tabs, disable the ability for the user to select any new map objects  to be added
        /// </summary>
        public void UpdateEditorPanelVisibility()
        {
            editorPanel.IsEnabled = tabs.Items.Count > 0; // at least 1 tabs for now
        }

        private void Tabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (multiBoard.SelectedBoard == null)
                return;

            lock (multiBoard)
            {
                MultiBoard_ReturnToSelectionState();

                if (tabs.SelectedItem != null)
                {
                    System.Windows.Controls.TabItem selectedTab = (System.Windows.Controls.TabItem)tabs.SelectedItem;

                    multiBoard.SelectedBoard = ((TabItemContainer)selectedTab.Tag).Board;

                    ApplicationSettings.lastDefaultLayer = multiBoard.SelectedBoard.SelectedLayerIndex;

                    ribbon.SetLayers(multiBoard.SelectedBoard.Layers);
                    ribbon.SetSelectedLayer(multiBoard.SelectedBoard.SelectedLayerIndex, multiBoard.SelectedBoard.SelectedPlatform, multiBoard.SelectedBoard.SelectedAllLayers, multiBoard.SelectedBoard.SelectedAllPlatforms);
                    ribbon.SetHasMinimap(multiBoard.SelectedBoard.MinimapRectangle != null);

                    ParseVisibleEditedTypes();
                } else
                {
                    multiBoard.SelectedBoard = null;
                }
                multiBoard.Focus();
            }
        }
        #endregion

        #region Ribbon Debug Handlers
        /// <summary>
        /// Show map '/info' handlers
        /// </summary>
        private void Ribbon_ShowMapPropertiesClicked()
        {
            if (multiBoard.SelectedBoard == null)
                return;
            List<WzImageProperty> unsupportedProp = multiBoard.SelectedBoard.MapInfo.unsupportedInfoProperties;

            StringBuilder sb = new StringBuilder();
            int i = 1;
            foreach (WzImageProperty imgProp in unsupportedProp)
            {
                sb.Append(i).Append(": ").Append(imgProp.Name);
                sb.Append(", val: ").Append(imgProp.WzValue != null ? imgProp.WzValue.ToString() : Environment.NewLine);
                sb.Append(Environment.NewLine);
                i++;
            }
            sb.Append(Environment.NewLine).Append("Fix it under MapInfo.cs");

            MessageBox.Show(sb.ToString(), "List of unsupported properties.");
        }
        #endregion


        #region Ribbon Handlers
        private string lastSaveLoc = null;

        public void Ribbon_ExportClicked()
        {
            SaveFileDialog ofd = new SaveFileDialog() { Title = "Select export location", Filter = "HaCreator Map File (*.ham)|*.ham" };
            if (lastSaveLoc != null)
                ofd.FileName = lastSaveLoc;
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            lastSaveLoc = ofd.FileName;
            // No need to lock, SerializeBoard locks only the critical areas to cut down on locked time
            try
            {
                File.WriteAllText(ofd.FileName, multiBoard.SelectedBoard.SerializationManager.SerializeBoard(true));
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Could not save: {0}\r\n\r\n{1}", e.Message, e.StackTrace));
            }
        }

        void Ribbon_UserObjsClicked()
        {
            lock (multiBoard)
            {
                new ManageUserObjects(multiBoard.UserObjects).ShowDialog();
                objPanel.OnL1Changed(UserObjectsManager.l1);
            }
        }

        void Ribbon_FinalizeClicked()
        {
            if (MessageBox.Show("This will finalize all footholds, removing their Tile bindings and clearing the Undo/Redo list in the process.\r\nContinue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                lock (multiBoard)
                {
                    new MapSaver(multiBoard.SelectedBoard).ActualizeFootholds();
                }
            }
        }

        void Ribbon_HaRepackerClicked()
        {
            WaitWindow ww = new WaitWindow("Opening HaRepacker...");
            ww.Show();
            Application.DoEvents();
            
            HaRepacker.Program.WzFileManager = new WzFileManager("", false);
            bool firstRun = HaRepacker.Program.PrepareApplication(false);
            HaRepacker.GUI.MainForm mf = new HaRepacker.GUI.MainForm(null, false, firstRun);
            mf.unloadAllToolStripMenuItem.Visible = false;
            mf.reloadAllToolStripMenuItem.Visible = false;
            foreach (KeyValuePair<string, WzFile> entry in Program.WzManager.wzFiles)
                mf.Interop_AddLoadedWzFileToManager(entry.Value);
            ww.EndWait();
            lock (multiBoard)
            {
                mf.ShowDialog();
            }
            HaRepacker.Program.EndApplication(false, false);
        }

        bool? getTypes(ItemTypes visibleTypes, ItemTypes editedTypes, ItemTypes type)
        {
            if ((editedTypes & type) == type)
            {
                return true;
            }
            else if ((visibleTypes & type) == type)
            {
                return (bool?)null;
            }
            else
            {
                return false;
            }
        }

        private void ParseVisibleEditedTypes()
        {
            ItemTypes visibleTypes = ApplicationSettings.theoreticalVisibleTypes = multiBoard.SelectedBoard.VisibleTypes;
            ItemTypes editedTypes = ApplicationSettings.theoreticalEditedTypes = multiBoard.SelectedBoard.EditedTypes;
            ribbon.SetVisibilityCheckboxes(getTypes(visibleTypes, editedTypes, ItemTypes.Tiles),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Objects),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.NPCs),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Mobs),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Reactors),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Portals),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Footholds),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Ropes),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Chairs),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.ToolTips),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Backgrounds),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Misc),
                                            getTypes(visibleTypes, editedTypes, ItemTypes.MirrorFieldData)
                                            );
        }

        void Ribbon_RandomTilesToggled(bool pressed)
        {
            ApplicationSettings.randomTiles = pressed;
            if (tilePanel != null)
                tilePanel.LoadTileSetList();
        }

        void Ribbon_SnappingToggled(bool pressed)
        {
            UserSettings.useSnapping = pressed;
        }

        void Ribbon_InfoModeToggled(bool pressed)
        {
            ApplicationSettings.InfoMode = pressed;
        }

        void Ribbon_RegenerateMinimapClicked()
        {
            if (multiBoard.SelectedBoard.RegenerateMinimap())
                MessageBox.Show("Minimap regenerated successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
            {
                MessageBox.Show("An error occured during minimap regeneration. The error has been logged. If possible, save the map report it via github.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrorLogger.Log(ErrorLevel.Critical, "error regenning minimap for map " + multiBoard.SelectedBoard.MapInfo.id.ToString());
            }
        }

        void Ribbon_MapSimulationClicked()
        {
            multiBoard.DeviceReady = false;


            Board selectedBoard = multiBoard.SelectedBoard;
            System.Windows.Controls.TabItem tab = (System.Windows.Controls.TabItem) tabs.SelectedItem;
            if (selectedBoard == null || tab == null)
                return;
            MapSimulator.MapSimulator mapSimulator = MapSimulator.MapSimulatorLoader.CreateAndShowMapSimulator(selectedBoard, (string) tab.Header);

            multiBoard.DeviceReady = true;
        }

        void Ribbon_ParallaxToggled(bool pressed)
        {
            UserSettings.emulateParallax = pressed;
        }

        void Ribbon_ShowMinimapToggled(bool pressed)
        {
            UserSettings.useMiniMap = pressed;
        }

        void SetTypes(ref ItemTypes newVisibleTypes, ref ItemTypes newEditedTypes, bool? x, ItemTypes type)
        {
            if (x.HasValue)
            {
                if (x.Value)
                {
                    newVisibleTypes ^= type;
                    newEditedTypes ^= type;
                }
            }
            else
            {
                newVisibleTypes ^= type;
            }
        }

        void Ribbon_ViewToggled(bool? tiles, bool? objs, bool? npcs, bool? mobs, bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField)
        {
            lock (multiBoard)
            {
                ItemTypes newVisibleTypes = 0;
                ItemTypes newEditedTypes = 0;
                SetTypes(ref newVisibleTypes, ref newEditedTypes, tiles, ItemTypes.Tiles);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, objs, ItemTypes.Objects);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, npcs, ItemTypes.NPCs);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, mobs, ItemTypes.Mobs);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, reactors, ItemTypes.Reactors);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, portals, ItemTypes.Portals);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, footholds, ItemTypes.Footholds);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, ropes, ItemTypes.Ropes);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, chairs, ItemTypes.Chairs);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, tooltips, ItemTypes.ToolTips);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, backgrounds, ItemTypes.Backgrounds);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, misc, ItemTypes.Misc);
                SetTypes(ref newVisibleTypes, ref newEditedTypes, mirrorField, ItemTypes.MirrorFieldData);

                ApplicationSettings.theoreticalVisibleTypes = newVisibleTypes;
                ApplicationSettings.theoreticalEditedTypes = newEditedTypes;
                if (multiBoard.SelectedBoard != null)
                {
                    InputHandler.ClearSelectedItems(multiBoard.SelectedBoard);
                    multiBoard.SelectedBoard.VisibleTypes = newVisibleTypes;
                    multiBoard.SelectedBoard.EditedTypes = newEditedTypes;
                }
            }
        }

        void Ribbon_ExitClicked()
        {
            if (CloseRequested != null)
            {
                CloseRequested.Invoke();
            }
        }

        void Ribbon_SettingsClicked()
        {
            lock (multiBoard)
            {
                new UserSettingsForm().ShowDialog();
            }
        }

        void Ribbon_HelpClicked()
        {
            string helpPath = Path.Combine(Application.StartupPath, "Help.htm");
            if (File.Exists(helpPath))
                Process.Start(helpPath);
            else
                MessageBox.Show("Help could not be shown because the help file (HRHelp.htm) was not found");
        }

        void Ribbon_AboutClicked()
        {
            new About().ShowDialog();
        }

        void Ribbon_RepackClicked()
        {
            lock (multiBoard)
            {
                Repack r = new Repack();
                r.ShowDialog();
            }
            if (Program.Restarting && CloseRequested != null)
            {
                CloseRequested.Invoke();
            }
        }

        void Ribbon_SaveClicked()
        {
            lock (multiBoard)
            {
                new Save(multiBoard.SelectedBoard).ShowDialog();
            }
        }

        public System.Windows.RoutedEventHandler[] MakeRightClickHandler()
        {
            return new System.Windows.RoutedEventHandler[] { 
                new System.Windows.RoutedEventHandler(MapEditInfo), 
                new System.Windows.RoutedEventHandler(MapAddVR), 
                new System.Windows.RoutedEventHandler(MapAddMinimap),
                 new System.Windows.RoutedEventHandler(CloseMapTab)
            };
        }

        void Ribbon_NewClicked()
        {
            LoadMap(new New(multiBoard, tabs, MakeRightClickHandler()));
        }

        void Ribbon_OpenClicked()
        {
            string mapNameFilter = null;
            Board currentSelectedBoard = multiBoard.SelectedBoard;
            if (currentSelectedBoard != null)
            {
                mapNameFilter = ( currentSelectedBoard.MapInfo.id / 10000).ToString(); // shows near-by maps relative to the current map opened in the Board
            }
            LoadMap(new Load(multiBoard, tabs, MakeRightClickHandler(), mapNameFilter));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tm">To map</param>
        public void LoadMap(int tm)
        {
            Load load = new Load(multiBoard, tabs, MakeRightClickHandler(), tm.ToString());

            LoadMap(load);
        }

        /// <summary>
        /// Loads a new map
        /// </summary>
        /// <param name="loader"></param>
        public void LoadMap(Form loader = null)
        {
            lock (multiBoard)
            {
                bool deviceLoadedThisTime = false;

                // load multiboard early before map
                if (!multiBoard.DeviceReady)
                {
                    ribbon.SetEnabled(true);
                    ribbon.SetOptions(UserSettings.useMiniMap, UserSettings.emulateParallax, UserSettings.useSnapping, ApplicationSettings.randomTiles, ApplicationSettings.InfoMode);
                    multiBoard.Start();
                    backupMan.Start();

                    deviceLoadedThisTime = true;
                }

                if (loader == null || loader.ShowDialog() == DialogResult.OK)
                {
                    if (deviceLoadedThisTime)
                    {
                        if (FirstMapLoaded != null)
                            FirstMapLoaded.Invoke();
                    }
                    multiBoard.SelectedBoard.SelectedPlatform = multiBoard.SelectedBoard.SelectedLayerIndex == -1 ? -1 : multiBoard.SelectedBoard.Layers[multiBoard.SelectedBoard.SelectedLayerIndex].zMList.ElementAt(0);
                    ribbon.SetLayers(multiBoard.SelectedBoard.Layers);
                    ribbon.SetSelectedLayer(multiBoard.SelectedBoard.SelectedLayerIndex, multiBoard.SelectedBoard.SelectedPlatform, multiBoard.SelectedBoard.SelectedAllLayers, multiBoard.SelectedBoard.SelectedAllPlatforms);
                    ribbon.SetHasMinimap(multiBoard.SelectedBoard.MinimapRectangle != null);
                    multiBoard.SelectedBoard.VisibleTypes = ApplicationSettings.theoreticalVisibleTypes;
                    multiBoard.SelectedBoard.EditedTypes = ApplicationSettings.theoreticalEditedTypes;
                    ParseVisibleEditedTypes();
                    multiBoard.Focus();
                }
            }
        }

        void ribbon_NewPlatformClicked()
        {
            lock (multiBoard)
            {
                NewPlatform dlg = new NewPlatform(new SortedSet<int>(multiBoard.SelectedBoard.Layers.Select(x => (IEnumerable<int>)x.zMList).Aggregate((x, y) => Enumerable.Concat(x, y))));
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;
                int zm = dlg.result;
                multiBoard.SelectedBoard.SelectedLayer.zMList.Add(zm);
                multiBoard.SelectedBoard.SelectedPlatform = zm;
                ribbon.SetLayers(multiBoard.SelectedBoard.Layers);
                ribbon.SetSelectedLayer(multiBoard.SelectedBoard.SelectedLayerIndex, multiBoard.SelectedBoard.SelectedPlatform, multiBoard.SelectedBoard.SelectedAllLayers, multiBoard.SelectedBoard.SelectedAllPlatforms);
            }
        }

        /// <summary>
        /// Edit map Physics
        /// </summary>
        private void Ribbon_EditMapPhysicsClicked()
        {
            MapPhysicsEditor editor = new MapPhysicsEditor();
            editor.ShowDialog();
        }
        #endregion

        #region Ribbon Layer Boxes
        private void SetLayer(int currentLayer, int currentPlatform, bool allLayers, bool allPlats)
        {
            multiBoard.SelectedBoard.SelectedLayerIndex = currentLayer;
            multiBoard.SelectedBoard.SelectedPlatform = currentPlatform;
            multiBoard.SelectedBoard.SelectedAllLayers = allLayers;
            multiBoard.SelectedBoard.SelectedAllPlatforms = allPlats;
            ApplicationSettings.lastDefaultLayer = currentLayer;
            ApplicationSettings.lastAllLayers = allLayers;
        }

        void ribbon_LayerViewChanged(int layer, int platform, bool allLayers, bool allPlats)
        {
            if (multiBoard.SelectedBoard == null)
                return;
            SetLayer(layer, platform, allLayers, allPlats);
            InputHandler.ClearSelectedItems(multiBoard.SelectedBoard);

        }
        #endregion

        public delegate void EmptyDelegate();

        public event EmptyDelegate CloseRequested;
        public event EmptyDelegate FirstMapLoaded;

        /// <summary>
        /// Creates the description of the selected item to be displayed on the top right corner of HaRibbon
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static string CreateItemDescription(BoardItem item)
        {
            const string firstLineSpacer = " ";

            StringBuilder sb = new StringBuilder();
            if (item is TileInstance)
            {
                sb.Append("[Tile]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append(((TileInfo)item.BaseInfo).tS).Append(@"\").Append(((TileInfo)item.BaseInfo).u).Append(@"\").Append(((TileInfo)item.BaseInfo).no);
            }
            else if (item is ObjectInstance)
            {
                sb.Append("[Object]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append(((ObjectInfo)item.BaseInfo).oS).Append(@"\").Append(((ObjectInfo)item.BaseInfo).l0).Append(@"\")
                    .Append(((ObjectInfo)item.BaseInfo).l1).Append(@"\").Append(((ObjectInfo)item.BaseInfo).l2);
            }
            else if (item is BackgroundInstance)
            {
                sb.Append("[Background]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append(((BackgroundInfo)item.BaseInfo).bS).Append(@"\").Append((((BackgroundInfo)item.BaseInfo).Type.ToString())).Append(@"\")
                    .Append(((BackgroundInfo)item.BaseInfo).no);
            }
            else if (item is PortalInstance)
            {
                sb.Append("[Portal]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("Name: ").Append(((PortalInstance)item).pn).Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("Type: ").Append(Tables.PortalTypeNames[((PortalInstance)item).pt]);
            }
            else if (item is MobInstance)
            {
                sb.Append("[Mob]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("Name: ").Append(((MobInfo)item.BaseInfo).Name).Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("ID: ").Append(((MobInfo)item.BaseInfo).ID);
            }
            else if (item is NpcInstance)
            {
                sb.Append("[Npc]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("Name: ").Append(((NpcInfo)item.BaseInfo).Name).Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("ID: ").Append(((NpcInfo)item.BaseInfo).ID);
            }
            else if (item is ReactorInstance)
            {
                sb.Append("[Reactor]").Append(Environment.NewLine);
                sb.Append(firstLineSpacer).Append("ID: ").Append(((ReactorInfo)item.BaseInfo).ID);
            }
            else if (item is FootholdAnchor)
            {
                sb.Append("[Foothold]");
            }
            else if (item is RopeAnchor)
            {
                RopeAnchor rope = (RopeAnchor)item;
                sb.Append(rope.ParentRope.ladder ? "[Ladder]" : "[Rope]");
            }
            else if (item is Chair)
            {
                sb.Append("[Chair]");
            }
            else if (item is ToolTipChar || item is ToolTipDot || item is ToolTipInstance)
            {
                sb.Append("[Tooltip]");
            }
            else if (item is INamedMisc misc)
            {
                sb.Append(misc.Name);
            } 
            else if (item is MirrorFieldData mirrorFieldData)
            {
                sb.Append("[MirrorFieldData]").Append(Environment.NewLine);
                sb.Append("Ground reflections for '").Append(mirrorFieldData.MirrorFieldDataType.ToString()).Append("'");
            }

            sb.Append(Environment.NewLine);
            sb.Append("width: ").Append(item.Width).Append(", height: ").Append(item.Height);

            return sb.ToString();
        }

        public void SetTilePanel(TilePanel tp)
        {
            this.tilePanel = tp;
        }

        public void SetObjPanel(ObjPanel op)
        {
            this.objPanel = op;
        }

        public void EnterEditMode(ItemTypes type)
        {
            multiBoard.SelectedBoard.EditedTypes = type;
            multiBoard.SelectedBoard.VisibleTypes |= type;
            ribbon.SetEnabled(false);
        }

        public void ExitEditMode()
        {
            multiBoard.SelectedBoard.EditedTypes = ApplicationSettings.theoreticalEditedTypes;
            multiBoard.SelectedBoard.VisibleTypes = ApplicationSettings.theoreticalVisibleTypes;
            ribbon.SetEnabled(true);
        }

        public MultiBoard MultiBoard
        {
            get
            {
                return multiBoard;
            }
        }

        public HaRibbon Ribbon
        {
            get
            {
                return ribbon;
            }
        }
    }
}
