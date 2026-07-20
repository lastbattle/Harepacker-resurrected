using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class TilePanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService hotSwapService;
        private bool isSelectingTileSet;

        public TilePanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
        }

        public event EventHandler SelectedIndexChanged;

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetTilePanel(this);
            tileSetList.ItemsSource = Program.InfoManager.TileSets.Keys.OrderBy(name => name).ToList();
        }

        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (hotSwapService != null)
                hotSwapService.TileSetChanged -= OnTileSetChanged;
            hotSwapService = refreshService;
            if (hotSwapService != null)
                hotSwapService.TileSetChanged += OnTileSetChanged;
        }

        private void OnTileSetChanged(object sender, TileSetChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => HandleTileSetChange(e));
                return;
            }
            HandleTileSetChange(e);
        }

        private void HandleTileSetChange(TileSetChangedEventArgs e)
        {
            string selected = tileSetList.SelectedItem as string;
            tileSetList.ItemsSource = Program.InfoManager.TileSets.Keys.OrderBy(name => name).ToList();

            if (e.ChangeType == AssetChangeType.Removed && selected == e.SetName)
            {
                tileImagesContainer.Clear();
                tileSetList.SelectedIndex = tileSetList.Items.Count > 0 ? 0 : -1;
            }
            else if (e.ChangeType == AssetChangeType.Modified && selected == e.SetName)
            {
                Program.InfoManager.RefreshTileSet(e.SetName);
                SetSelectedTileSet(e.SetName);
            }
            else if (selected != null && tileSetList.Items.Contains(selected))
            {
                tileSetList.SelectedItem = selected;
            }
        }

        private void TileBrowse_Click(object sender, RoutedEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                // TileSetBrowser remains WinForms during the staged dialog migration.
                using TileSetBrowser browser = new(SetSelectedTileSet);
                browser.ShowDialog();
            }
        }

        public void SetSelectedTileSet(string tileSet)
        {
            if (!Program.InfoManager.TileSets.ContainsKey(tileSet))
                return;
            isSelectingTileSet = true;
            tileSetList.SelectedItem = tileSet;
            isSelectingTileSet = false;
            LoadTileSetList();
        }

        private void TileSetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedIndexChanged?.Invoke(sender, EventArgs.Empty);
            if (!isSelectingTileSet)
                LoadTileSetList();
        }

        public void LoadTileSetList()
        {
            if (hcsm == null)
                return;

            lock (hcsm.MultiBoard)
            {
                if (tileSetList.SelectedItem is not string selectedSetName ||
                    !Program.InfoManager.TileSets.ContainsKey(selectedSetName))
                    return;

                tileImagesContainer.Clear();
                WzImage tileSetImage = Program.InfoManager.GetTileSet(selectedSetName);
                if (tileSetImage == null)
                    return;
                int? mag = InfoTool.GetOptionalInt(tileSetImage["info"]["mag"]);

                foreach (WzSubProperty category in tileSetImage.WzProperties.OfType<WzSubProperty>())
                {
                    if (category.Name == "info")
                        continue;

                    if (ApplicationSettings.randomTiles)
                    {
                        WzCanvasProperty canvas = category["0"] as WzCanvasProperty;
                        if (canvas == null)
                            continue;
                        TileInfo[] randomInfos = category.WzProperties
                            .Select(tile => TileInfo.Get(selectedSetName, category.Name, tile.Name, mag))
                            .ToArray();
                        tileImagesContainer.Add(canvas.GetLinkedWzCanvasBitmap(), category.Name, randomInfos);
                    }
                    else
                    {
                        foreach (WzCanvasProperty tile in category.WzProperties.OfType<WzCanvasProperty>())
                        {
                            TileInfo info = TileInfo.Get(selectedSetName, category.Name, tile.Name, mag);
                            tileImagesContainer.Add(tile.GetLinkedWzCanvasBitmap(), $"{category.Name}/{tile.Name}", info);
                        }
                    }
                }
            }
        }

        private void TileImagesContainer_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            if (hcsm?.MultiBoard.SelectedBoard == null ||
                e.Item.Tag is not TileInfo && e.Item.Tag is not TileInfo[])
                return;

            lock (hcsm.MultiBoard)
            {
                if (!hcsm.MultiBoard.AssertLayerSelected())
                    return;

                TileInfo selectedInfo = e.Item.Tag is TileInfo[] randomInfos ? randomInfos[0] : (TileInfo)e.Item.Tag;
                Layer layer = hcsm.MultiBoard.SelectedBoard.SelectedLayer;
                if (layer.tS != null && selectedInfo.tS != layer.tS)
                {
                    MessageBoxResult result = MessageBox.Show(
                        EditorPanelLocalizer.Text("Confirm_ChangeTileSet", "This will change the active layer's tile set. Continue?"),
                        EditorPanelLocalizer.Text("Title_ChangeTileSet", "Change tile set"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                        return;

                    List<UndoRedoAction> actions = new()
                    {
                        UndoRedoManager.LayerTSChanged(layer, layer.tS, selectedInfo.tS)
                    };
                    layer.ReplaceTS(selectedInfo.tS);
                    hcsm.MultiBoard.SelectedBoard.UndoRedoMan.AddUndoBatch(actions);
                }

                hcsm.EnterEditMode(ItemTypes.Tiles);
                if (e.Item.Tag is TileInfo[] infos)
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetRandomTilesMode(infos);
                else
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((TileInfo)e.Item.Tag);
                hcsm.MultiBoard.Focus();
            }
        }
    }
}
