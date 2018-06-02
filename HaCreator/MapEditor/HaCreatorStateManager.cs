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
using HaCreator.ThirdParty.TabPages;
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

namespace HaCreator.MapEditor
{
    public class HaCreatorStateManager
    {
        private MultiBoard multiBoard;
        private HaRibbon ribbon;
        private PageCollection tabs;
        private InputHandler input;
        private TilePanel tilePanel;
        private ObjPanel objPanel;
        public BackupManager backupMan;

        public HaCreatorStateManager(MultiBoard multiBoard, HaRibbon ribbon, PageCollection tabs, InputHandler input)
        {
            this.multiBoard = multiBoard;
            this.ribbon = ribbon;
            this.tabs = tabs;
            this.input = input;
            this.backupMan = new BackupManager(multiBoard, input, this, tabs);

            this.ribbon.NewClicked += ribbon_NewClicked;
            this.ribbon.OpenClicked += ribbon_OpenClicked;
            this.ribbon.SaveClicked += ribbon_SaveClicked;
            this.ribbon.RepackClicked += ribbon_RepackClicked;
            this.ribbon.AboutClicked += ribbon_AboutClicked;
            this.ribbon.HelpClicked += ribbon_HelpClicked;
            this.ribbon.SettingsClicked += ribbon_SettingsClicked;
            this.ribbon.ExitClicked += ribbon_ExitClicked;
            this.ribbon.ViewToggled += ribbon_ViewToggled;
            this.ribbon.ShowMinimapToggled += ribbon_ShowMinimapToggled;
            this.ribbon.ParallaxToggled += ribbon_ParallaxToggled;
            this.ribbon.LayerViewChanged += ribbon_LayerViewChanged;
            this.ribbon.MapSimulationClicked += ribbon_MapSimulationClicked;
            this.ribbon.RegenerateMinimapClicked += ribbon_RegenerateMinimapClicked;
            this.ribbon.SnappingToggled += ribbon_SnappingToggled;
            this.ribbon.RandomTilesToggled += ribbon_RandomTilesToggled;
            this.ribbon.InfoModeToggled += ribbon_InfoModeToggled;
            this.ribbon.HaRepackerClicked += ribbon_HaRepackerClicked;
            this.ribbon.FinalizeClicked += ribbon_FinalizeClicked;
            this.ribbon.NewPlatformClicked += ribbon_NewPlatformClicked;
            this.ribbon.UserObjsClicked += ribbon_UserObjsClicked;
            this.ribbon.ExportClicked += ribbon_ExportClicked;
            this.ribbon.RibbonKeyDown += multiBoard.DxContainer_KeyDown;

            this.tabs.CurrentPageChanged += tabs_CurrentPageChanged;
            this.tabs.PageClosing += tabs_PageClosing;
            this.tabs.PageRemoved += tabs_PageRemoved;

            this.multiBoard.OnBringToFrontClicked += multiBoard_OnBringToFrontClicked;
            this.multiBoard.OnEditBaseClicked += multiBoard_OnEditBaseClicked;
            this.multiBoard.OnEditInstanceClicked += multiBoard_OnEditInstanceClicked;
            this.multiBoard.OnLayerTSChanged += multiBoard_OnLayerTSChanged;
            this.multiBoard.OnSendToBackClicked += multiBoard_OnSendToBackClicked;
            this.multiBoard.ReturnToSelectionState += multiBoard_ReturnToSelectionState;
            this.multiBoard.SelectedItemChanged += multiBoard_SelectedItemChanged;
            this.multiBoard.MouseMoved += multiBoard_MouseMoved;
            this.multiBoard.ImageDropped += multiBoard_ImageDropped;
            this.multiBoard.ExportRequested += ribbon_ExportClicked;
            this.multiBoard.LoadRequested += ribbon_OpenClicked;
            this.multiBoard.CloseTabRequested += multiBoard_CloseTabRequested;
            this.multiBoard.SwitchTabRequested += multiBoard_SwitchTabRequested;
            this.multiBoard.BackupCheck += multiBoard_BackupCheck;
            this.multiBoard.BoardRemoved += multiBoard_BoardRemoved;
            this.multiBoard.MinimapStateChanged += multiBoard_MinimapStateChanged;

            multiBoard.Visible = false;
            ribbon.SetEnabled(false);
        }

        public static int PositiveMod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        void multiBoard_SwitchTabRequested(object sender, bool reverse)
        {
            tabs.CurrentPage = tabs[PositiveMod(tabs.IndexOf(tabs.CurrentPage) + (reverse ? -1 : 1), tabs.Count)];
        }

        void multiBoard_CloseTabRequested()
        {
            tabs.CurrentPage.Close();
        }

        #region MultiBoard Events
        void multiBoard_MinimapStateChanged(object sender, bool hasMm)
        {
            ribbon.SetHasMinimap(hasMm);
        }

        void multiBoard_BoardRemoved(object sender, EventArgs e)
        {
            Board board = (Board)sender;
            backupMan.DeleteBackup(board.UniqueID);
        }

        private void tabs_PageClosing(HaCreator.ThirdParty.TabPages.TabPage page, ref bool cancel)
        {
            if (MessageBox.Show("Are you sure you want to close this map?", "Close", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                cancel = true;
        }

        void tabs_PageRemoved(ThirdParty.TabPages.TabPage page)
        {
            Board board = (Board)page.Tag;
            board.Dispose();
        }

        void multiBoard_BackupCheck()
        {
            try
            {
                backupMan.BackupCheck();
            }
            catch (Exception e)
            {
                HaRepackerLib.Warning.Error(string.Format("Backup failed! Error:{0}\r\n{1}", e.Message, e.StackTrace));
            }
        }

        void multiBoard_ImageDropped(Board selectedBoard, System.Drawing.Bitmap bmp, string name, Microsoft.Xna.Framework.Point pos)
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

        void multiBoard_MouseMoved(Board selectedBoard, Microsoft.Xna.Framework.Point oldPos, Microsoft.Xna.Framework.Point newPos, Microsoft.Xna.Framework.Point currPhysicalPos)
        {
            ribbon.SetMousePos(newPos.X, newPos.Y, currPhysicalPos.X, currPhysicalPos.Y);
        }

        void multiBoard_SelectedItemChanged(BoardItem selectedItem)
        {
            if (selectedItem != null)
            {
                ribbon.SetItemDesc(CreateItemDescription(selectedItem, "\n"));
            }
            else
            {
                ribbon.SetItemDesc("");
            }
        }

        void multiBoard_ReturnToSelectionState()
        {
            // No need to lock because SelectionMode() and ExitEditMode() are both thread-safe
            multiBoard.SelectedBoard.Mouse.SelectionMode();
            ExitEditMode();
            multiBoard.Focus();
        }

        void multiBoard_OnSendToBackClicked(BoardItem boardRefItem)
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

        void multiBoard_OnLayerTSChanged(Layer layer)
        {
            ribbon.SetLayer(layer);
        }

        void multiBoard_OnEditInstanceClicked(BoardItem item)
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
                else if (item is RopeAnchor)
                {
                    new RopeInstanceEditor((RopeAnchor)item).ShowDialog();
                }
                else if (item is LifeInstance)
                {
                    new LifeInstanceEditor((LifeInstance)item).ShowDialog();
                }
                else if (item is ReactorInstance)
                {
                    new ReactorInstanceEditor((ReactorInstance)item).ShowDialog();
                }
                else if (item is BackgroundInstance)
                {
                    new BackgroundInstanceEditor((BackgroundInstance)item).ShowDialog();
                }
                else if (item is PortalInstance)
                {
                    new PortalInstanceEditor((PortalInstance)item).ShowDialog();
                }
                else if (item is ToolTipInstance)
                {
                    new TooltipInstanceEditor((ToolTipInstance)item).ShowDialog();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("An error occurred while presenting the instance editor for {0}:\r\n{1}", item.GetType().Name, e.ToString()));
            }
        }

        void multiBoard_OnEditBaseClicked(BoardItem item)
        {
            //TODO
        }

        void multiBoard_OnBringToFrontClicked(BoardItem boardRefItem)
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
        private void mapEditInfo(object sender, EventArgs e)
        {
            Board selectedBoard = (Board)((ToolStripMenuItem)sender).Tag;
            lock (selectedBoard.ParentControl)
            {
                new InfoEditor(selectedBoard, selectedBoard.MapInfo, multiBoard).ShowDialog();
                if (selectedBoard.ParentControl.SelectedBoard == selectedBoard)
                    selectedBoard.ParentControl.AdjustScrollBars();
            }
        }

        private void mapAddVR(object sender, EventArgs e)
        {
            Board selectedBoard = (Board)((ToolStripMenuItem)sender).Tag;
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

        private void mapAddMinimap(object sender, EventArgs e)
        {
            Board selectedBoard = (Board)((ToolStripMenuItem)sender).Tag;
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

        void tabs_CurrentPageChanged(HaCreator.ThirdParty.TabPages.TabPage currentPage, HaCreator.ThirdParty.TabPages.TabPage previousPage)
        {
            if (previousPage == null)
            {
                return;
            }
            lock (multiBoard)
            {
                multiBoard_ReturnToSelectionState();
                multiBoard.SelectedBoard = (Board)currentPage.Tag;
                ApplicationSettings.lastDefaultLayer = multiBoard.SelectedBoard.SelectedLayerIndex;
                ribbon.SetLayers(multiBoard.SelectedBoard.Layers);
                ribbon.SetSelectedLayer(multiBoard.SelectedBoard.SelectedLayerIndex, multiBoard.SelectedBoard.SelectedPlatform, multiBoard.SelectedBoard.SelectedAllLayers, multiBoard.SelectedBoard.SelectedAllPlatforms);
                ribbon.SetHasMinimap(multiBoard.SelectedBoard.MinimapRectangle != null);
                ParseVisibleEditedTypes();
                multiBoard.Focus();
            }
        }
        #endregion

        #region Ribbon Handlers
        private string lastSaveLoc = null;

        public void ribbon_ExportClicked()
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
                HaRepackerLib.Warning.Error(string.Format("Could not save: {0}\r\n\r\n{1}", e.Message, e.StackTrace));
            }
        }

        void ribbon_UserObjsClicked()
        {
            lock (multiBoard)
            {
                new ManageUserObjects(multiBoard.UserObjects).ShowDialog();
                objPanel.OnL1Changed(UserObjectsManager.l1);
            }
        }

        void ribbon_FinalizeClicked()
        {
            if (MessageBox.Show("This will finalize all footholds, removing their Tile bindings and clearing the Undo/Redo list in the process.\r\nContinue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                lock (multiBoard)
                {
                    new MapSaver(multiBoard.SelectedBoard).ActualizeFootholds();
                }
            }
        }

        void ribbon_HaRepackerClicked()
        {
            WaitWindow ww = new WaitWindow("Opening HaRepacker...");
            ww.Show();
            Application.DoEvents();
            HaRepacker.Program.WzMan = new HaRepackerLib.WzFileManager();
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
                                            getTypes(visibleTypes, editedTypes, ItemTypes.Misc));
        }
        
        void ribbon_RandomTilesToggled(bool pressed)
        {
            ApplicationSettings.randomTiles = pressed;
            if (tilePanel != null)
                tilePanel.LoadTileSetList();
        }

        void ribbon_SnappingToggled(bool pressed)
        {
            UserSettings.useSnapping = pressed;
        }

        void ribbon_InfoModeToggled(bool pressed)
        {
            ApplicationSettings.InfoMode = pressed;
        }

        void ribbon_RegenerateMinimapClicked()
        {
            if (multiBoard.SelectedBoard.RegenerateMinimap())
                MessageBox.Show("Minimap regenerated successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
            {
                MessageBox.Show("An error occured during minimap regeneration. The error has been logged. If possible, save the map and send it to" + ApplicationSettings.AuthorEmail, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrorLogger.Log(ErrorLevel.Critical, "error regenning minimap for map " + multiBoard.SelectedBoard.MapInfo.id.ToString());
            }
        }

        void ribbon_MapSimulationClicked()
        {
            multiBoard.DeviceReady = false;
            MapSimulator.MapSimulator.CreateMapSimulator(multiBoard.SelectedBoard).ShowDialog();
            multiBoard.DeviceReady = true;
        }

        void ribbon_ParallaxToggled(bool pressed)
        {
            UserSettings.emulateParallax = pressed;
        }

        void ribbon_ShowMinimapToggled(bool pressed)
        {
            UserSettings.useMiniMap = pressed;
        }

        void setTypes(ref ItemTypes newVisibleTypes, ref ItemTypes newEditedTypes, bool? x, ItemTypes type)
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

        void ribbon_ViewToggled(bool? tiles, bool? objs, bool? npcs, bool? mobs, bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs, bool? tooltips, bool? backgrounds, bool? misc)
        {
            lock (multiBoard)
            {
                ItemTypes newVisibleTypes = 0;
                ItemTypes newEditedTypes = 0;
                setTypes(ref newVisibleTypes, ref newEditedTypes, tiles, ItemTypes.Tiles);
                setTypes(ref newVisibleTypes, ref newEditedTypes, objs, ItemTypes.Objects);
                setTypes(ref newVisibleTypes, ref newEditedTypes, npcs, ItemTypes.NPCs);
                setTypes(ref newVisibleTypes, ref newEditedTypes, mobs, ItemTypes.Mobs);
                setTypes(ref newVisibleTypes, ref newEditedTypes, reactors, ItemTypes.Reactors);
                setTypes(ref newVisibleTypes, ref newEditedTypes, portals, ItemTypes.Portals);
                setTypes(ref newVisibleTypes, ref newEditedTypes, footholds, ItemTypes.Footholds);
                setTypes(ref newVisibleTypes, ref newEditedTypes, ropes, ItemTypes.Ropes);
                setTypes(ref newVisibleTypes, ref newEditedTypes, chairs, ItemTypes.Chairs);
                setTypes(ref newVisibleTypes, ref newEditedTypes, tooltips, ItemTypes.ToolTips);
                setTypes(ref newVisibleTypes, ref newEditedTypes, backgrounds, ItemTypes.Backgrounds);
                setTypes(ref newVisibleTypes, ref newEditedTypes, misc, ItemTypes.Misc);
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

        void ribbon_ExitClicked()
        {
            if (CloseRequested != null)
            {
                CloseRequested.Invoke();
            }
        }

        void ribbon_SettingsClicked()
        {
            lock (multiBoard)
            {
                new UserSettingsForm().ShowDialog();
            }
        }

        void ribbon_HelpClicked()
        {
            string helpPath = Path.Combine(Application.StartupPath, "Help.htm");
            if (File.Exists(helpPath))
                Process.Start(helpPath);
            else
                HaRepackerLib.Warning.Error("Help could not be shown because the help file (HRHelp.htm) was not found");
        }

        void ribbon_AboutClicked()
        {
            new About().ShowDialog();
        }

        void ribbon_RepackClicked()
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

        void ribbon_SaveClicked()
        {
            lock(multiBoard)
            {
                new Save(multiBoard.SelectedBoard).ShowDialog();
            }
        }

        public EventHandler[] MakeRightClickHandler()
        {
            return new EventHandler[] { new EventHandler(mapEditInfo), new EventHandler(mapAddVR), new EventHandler(mapAddMinimap) };
        }

        void ribbon_NewClicked()
        {
            LoadMap(new New(multiBoard, tabs, MakeRightClickHandler()));
        }

        void ribbon_OpenClicked()
        {
            LoadMap(new Load(multiBoard, tabs, MakeRightClickHandler()));
        }

        public void LoadMap(Form loader = null)
        {
            lock (multiBoard)
            {
                if (loader == null || loader.ShowDialog() == DialogResult.OK)
                {
                    if (!multiBoard.DeviceReady)
                    {
                        ribbon.SetEnabled(true);
                        ribbon.SetOptions(UserSettings.useMiniMap, UserSettings.emulateParallax, UserSettings.useSnapping, ApplicationSettings.randomTiles, ApplicationSettings.InfoMode);
                        if (FirstMapLoaded != null)
                            FirstMapLoaded.Invoke();
                        multiBoard.Start();
                        backupMan.Start();
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
                NewPlatform dlg = new NewPlatform(new SortedSet<int>(multiBoard.SelectedBoard.Layers.Select(x => (IEnumerable<int>)x.zMList).Aggregate((x,y) => Enumerable.Concat(x, y))));
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;
                int zm = dlg.result;
                multiBoard.SelectedBoard.SelectedLayer.zMList.Add(zm);
                multiBoard.SelectedBoard.SelectedPlatform = zm;
                ribbon.SetLayers(multiBoard.SelectedBoard.Layers);
                ribbon.SetSelectedLayer(multiBoard.SelectedBoard.SelectedLayerIndex, multiBoard.SelectedBoard.SelectedPlatform, multiBoard.SelectedBoard.SelectedAllLayers, multiBoard.SelectedBoard.SelectedAllPlatforms);
            }
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

        public static string CreateItemDescription(BoardItem item, string lineBreak)
        {
            if (item is TileInstance)
            {
                return "Tile:" + lineBreak + ((TileInfo)item.BaseInfo).tS + @"\" + ((TileInfo)item.BaseInfo).u + @"\" + ((TileInfo)item.BaseInfo).no;
            }
            else if (item is ObjectInstance)
            {
                return "Object:" + lineBreak + ((ObjectInfo)item.BaseInfo).oS + @"\" + ((ObjectInfo)item.BaseInfo).l0 + @"\" + ((ObjectInfo)item.BaseInfo).l1 + @"\" + ((ObjectInfo)item.BaseInfo).l2;
            }
            else if (item is BackgroundInstance)
            {
                return "Background:" + lineBreak + ((BackgroundInfo)item.BaseInfo).bS + @"\" + (((BackgroundInfo)item.BaseInfo).ani ? "ani" : "back") + @"\" + ((BackgroundInfo)item.BaseInfo).no;
            }
            else if (item is PortalInstance)
            {
                return "Portal:" + lineBreak + "Name: " + ((PortalInstance)item).pn + lineBreak + "Type: " + Tables.PortalTypeNames[((PortalInstance)item).pt];
            }
            else if (item is MobInstance)
            {
                return "Mob:" + lineBreak + "Name: " + ((MobInfo)item.BaseInfo).Name + lineBreak + "ID: " + ((MobInfo)item.BaseInfo).ID;
            }
            else if (item is NpcInstance)
            {
                return "Npc:" + lineBreak + "Name: " + ((NpcInfo)item.BaseInfo).Name + lineBreak + "ID: " + ((NpcInfo)item.BaseInfo).ID;
            }
            else if (item is ReactorInstance)
            {
                return "Reactor:" + lineBreak + "ID: " + ((ReactorInfo)item.BaseInfo).ID;
            }
            else if (item is FootholdAnchor)
            {
                return "Foothold";
            }
            else if (item is RopeAnchor)
            {
                return ((RopeAnchor)item).ParentRope.ladder ? "Ladder" : "Rope";
            }
            else if (item is Chair)
            {
                return "Chair";
            }
            else if (item is ToolTipChar || item is ToolTipDot || item is ToolTipInstance)
            {
                return "Tooltip";
            }
            else if (item is INamedMisc)
            {
                return ((INamedMisc)item).Name;
            }
            else
            {
                return "";
            }
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
