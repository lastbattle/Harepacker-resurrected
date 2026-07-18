using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HaCreator.GUI.EditorPanels
{
    public partial class ObjectViewerPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private Board _currentBoard;
        private bool _suppressSelectionSync;
        private readonly DispatcherTimer _refreshTimer;
        private readonly ContextMenu _contextMenu = new();
        private AssetGalleryItem _contextItem;

        public ObjectViewerPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            objectGallery.AllowMultipleSelection = true;
            objectGallery.ShowSearchBox = false;
            objectGallery.ItemActivated += ObjectGallery_ItemActivated;
            objectGallery.ContextRequested += ObjectGallery_ContextRequested;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                RefreshObjectGallery();
            };
            BuildContextMenu();
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetObjectViewerPanel(this);
            hcsm.MultiBoard.SelectedItemChanged += OnBoardSelectionChanged;
            OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
        }

        public void OnBoardChanged(Board newBoard)
        {
            _currentBoard = newBoard;
            RefreshObjectGallery();
        }

        private void BuildContextMenu()
        {
            _contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectOnBoard", "Select on board"), SelectOnBoard_Click));
            _contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_JumpToObject", "Jump to object"), JumpToObject_Click));
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_EditProperties", "Edit properties…"), EditProperties_Click));
            _contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_Delete", "Delete"), Delete_Click));
            _contextMenu.Items.Add(new Separator());
            _contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectAllOfType", "Select all of type"), SelectAllOfType_Click));
            MenuItem selectLayer = CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectAllInLayer", "Select all in layer"), SelectAllInLayer_Click);
            selectLayer.Name = "SelectLayerMenuItem";
            _contextMenu.Items.Add(selectLayer);
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            MenuItem item = new() { Header = header };
            item.Click += handler;
            return item;
        }

        private void RefreshObjectGallery()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshObjectGallery));
                return;
            }

            objectGallery.Clear();
            if (_currentBoard == null || hcsm?.MultiBoard == null)
            {
                UpdateStatistics();
                return;
            }

            try
            {
                lock (hcsm.MultiBoard)
                {
                    foreach (BoardItem item in EnumerateBoardItems(_currentBoard))
                        objectGallery.Add(BuildItemLabel(item), item);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"ObjectViewerPanel.RefreshObjectGallery error: {exception.Message}");
            }

            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private static IEnumerable<BoardItem> EnumerateBoardItems(Board board)
        {
            if (board?.BoardItems == null)
                return Enumerable.Empty<BoardItem>();

            return board.BoardItems.TileObjs.Cast<BoardItem>()
                .Concat(board.BoardItems.BackBackgrounds)
                .Concat(board.BoardItems.FrontBackgrounds)
                .Concat(board.BoardItems.NPCs)
                .Concat(board.BoardItems.Mobs)
                .Concat(board.BoardItems.Reactors)
                .Concat(board.BoardItems.Portals)
                .Concat(board.BoardItems.FHAnchors)
                .Concat(board.BoardItems.RopeAnchors)
                .Concat(board.BoardItems.Chairs)
                .Concat(board.BoardItems.ToolTips.Cast<BoardItem>())
                .Concat(board.BoardItems.CharacterToolTips.Cast<BoardItem>())
                .Concat(board.BoardItems.MiscItems)
                .Concat(board.BoardItems.MirrorFieldDatas);
        }

        private string BuildItemLabel(BoardItem item)
        {
            string category = GetCategory(item);
            int? layer = GetLayer(item);
            string layerText = layer.HasValue ? EditorPanelLocalizer.Format("Format_Layer", layer.Value) : string.Empty;
            return EditorPanelLocalizer.Format("Format_ObjectSummary", category, layerText, GetShortDescription(item), item.X, item.Y);
        }

        private static string GetCategory(BoardItem item) => item switch
        {
            TileInstance => EditorPanelLocalizer.Text("ObjectType_Tile", "Tile"),
            ObjectInstance => EditorPanelLocalizer.Text("ObjectType_Object", "Object"),
            BackgroundInstance => EditorPanelLocalizer.Text("ObjectType_Background", "Background"),
            NpcInstance => "NPC",
            MobInstance => EditorPanelLocalizer.Text("ObjectType_Mob", "Mob"),
            ReactorInstance => EditorPanelLocalizer.Text("ObjectType_Reactor", "Reactor"),
            PortalInstance => EditorPanelLocalizer.Text("ObjectType_Portal", "Portal"),
            FootholdAnchor => EditorPanelLocalizer.Text("ObjectType_Foothold", "Foothold"),
            RopeAnchor => EditorPanelLocalizer.Text("ObjectType_RopeLadder", "Rope/Ladder"),
            Chair => EditorPanelLocalizer.Text("ObjectType_Chair", "Chair"),
            ToolTipInstance or ToolTipChar => EditorPanelLocalizer.Text("ObjectType_Tooltip", "Tooltip"),
            MirrorFieldData => EditorPanelLocalizer.Text("ObjectType_MirrorField", "Mirror field"),
            _ => EditorPanelLocalizer.Text("ObjectType_Misc", "Misc")
        };

        private static int? GetLayer(BoardItem item) => item switch
        {
            TileInstance tile => tile.Layer?.LayerNumber,
            ObjectInstance obj => obj.Layer?.LayerNumber,
            FootholdAnchor foothold => foothold.LayerNumber,
            _ => null
        };

        private static string GetShortDescription(BoardItem item)
        {
            if (item is TileInstance tile)
            {
                var info = (HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo;
                return $"{info.tS}\\{info.u}\\{info.no}";
            }
            if (item is ObjectInstance obj)
            {
                var info = (HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo;
                return $"{info.oS}\\{info.l0}\\{info.l1}\\{info.l2}";
            }
            if (item is BackgroundInstance background)
            {
                var info = (HaCreator.MapEditor.Info.BackgroundInfo)background.BaseInfo;
                return $"{info.bS}\\{info.Type}\\{info.no}";
            }
            if (item is PortalInstance portal)
                return $"{portal.pn} ({portal.pt})";
            if (item is MobInstance mob)
            {
                var info = (HaCreator.MapEditor.Info.MobInfo)mob.BaseInfo;
                return $"{info.Name} ({info.ID})";
            }
            if (item is NpcInstance npc)
            {
                var info = (HaCreator.MapEditor.Info.NpcInfo)npc.BaseInfo;
                return $"{info.StringName} ({info.ID})";
            }
            if (item is ReactorInstance reactor)
                return $"Reactor {((HaCreator.MapEditor.Info.ReactorInfo)reactor.BaseInfo).ID}";
            if (item is RopeAnchor rope)
                return rope.ParentRope?.ladder == true ? "Ladder" : "Rope";
            if (item is MirrorFieldData mirror)
                return $"Mirror ({mirror.MirrorFieldDataType})";
            if (item is INamedMisc misc)
                return misc.Name;
            return item.GetType().Name;
        }

        private void ObjectGallery_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            if (_suppressSelectionSync || e.Item?.Tag is not BoardItem selectedItem || _currentBoard == null)
                return;

            _suppressSelectionSync = true;
            try
            {
                lock (hcsm.MultiBoard)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                        InputHandler.ClearSelectedItems(_currentBoard);

                    foreach (AssetGalleryItem galleryItem in objectGallery.SelectedItems)
                        if (galleryItem.Tag is BoardItem item)
                            item.Selected = true;

                    selectedItem.Selected = true;
                    CenterViewOnItem(selectedItem);
                    hcsm.MultiBoard.Focus();
                }
                UpdateStatistics();
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        private void ObjectGallery_ContextRequested(object sender, AssetGalleryItemEventArgs e)
        {
            if (e.Item?.Tag is not BoardItem item)
                return;
            _contextItem = e.Item;
            if (_contextMenu.Items.OfType<MenuItem>().FirstOrDefault(menu => menu.Name == "SelectLayerMenuItem") is MenuItem layerMenu)
                layerMenu.IsEnabled = GetLayer(item).HasValue;
            _contextMenu.PlacementTarget = objectGallery;
            _contextMenu.IsOpen = true;
        }

        private void OnBoardSelectionChanged(BoardItem selectedItem)
        {
            if (_suppressSelectionSync)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnBoardSelectionChanged(selectedItem)));
                return;
            }

            _suppressSelectionSync = true;
            try
            {
                UpdateSelectionFromBoard();
                UpdateStatistics();
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        private void UpdateSelectionFromBoard()
        {
            if (_currentBoard == null || hcsm == null)
                return;
            List<BoardItem> selected;
            lock (hcsm.MultiBoard)
                selected = _currentBoard.SelectedItems.ToList();
            objectGallery.SelectByTags(selected.Cast<object>());
        }

        private void CenterViewOnItem(BoardItem item)
        {
            if (_currentBoard == null || hcsm == null)
                return;
            lock (hcsm.MultiBoard)
            {
                float zoom = _currentBoard.Zoom;
                int viewWidth = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Width / zoom);
                int viewHeight = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Height / zoom);
                _currentBoard.hScroll = Math.Max(0, item.X + _currentBoard.CenterPoint.X - viewWidth / 2);
                _currentBoard.vScroll = Math.Max(0, item.Y + _currentBoard.CenterPoint.Y - viewHeight / 2);
            }
        }

        private void UpdateStatistics()
        {
            if (_currentBoard == null || hcsm == null)
            {
                totalCountText.Text = EditorPanelLocalizer.Format("Format_Total", 0);
                selectedCountText.Text = EditorPanelLocalizer.Format("Format_Selected", 0);
                return;
            }
            lock (hcsm.MultiBoard)
            {
                totalCountText.Text = EditorPanelLocalizer.Format("Format_Total", _currentBoard.BoardItems.Count);
                selectedCountText.Text = EditorPanelLocalizer.Format("Format_Selected", _currentBoard.SelectedItems.Count);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            objectGallery.SetFilter(searchBox.Text);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshObjectGallery();

        private void Viewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && objectGallery.SelectedItem?.Tag is BoardItem item)
            {
                CenterViewOnItem(item);
                e.Handled = true;
            }
        }

        private BoardItem ContextBoardItem => _contextItem?.Tag as BoardItem;

        private void SelectOnBoard_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is not BoardItem item)
                return;
            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(_currentBoard);
                item.Selected = true;
                hcsm.MultiBoard.Focus();
            }
            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private void JumpToObject_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is BoardItem item)
                CenterViewOnItem(item);
        }

        private void EditProperties_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is BoardItem item)
                hcsm.MultiBoard.EditInstanceClicked(item);
        }

        private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelectedItems();

        private void DeleteSelectedItems()
        {
            if (_currentBoard == null)
                return;
            lock (hcsm.MultiBoard)
            {
                List<BoardItem> items = _currentBoard.SelectedItems.ToList();
                if (items.Count == 0 && ContextBoardItem is BoardItem contextItem)
                    items.Add(contextItem);
                List<UndoRedoAction> actions = new();
                foreach (BoardItem item in items)
                {
                    if (item is ToolTipDot or MiscDot or VRDot or MinimapDot)
                        continue;
                    item.RemoveItem(actions);
                }
                if (actions.Count > 0)
                    _currentBoard.UndoRedoMan.AddUndoBatch(actions);
            }
            RefreshObjectGallery();
        }

        private void SelectAllOfType_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is not BoardItem source)
                return;
            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(_currentBoard);
                foreach (BoardItem item in EnumerateBoardItems(_currentBoard).Where(item => item.Type == source.Type))
                    item.Selected = true;
                hcsm.MultiBoard.Focus();
            }
            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private void SelectAllInLayer_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is not BoardItem source || GetLayer(source) is not int layer)
                return;
            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(_currentBoard);
                foreach (BoardItem item in EnumerateBoardItems(_currentBoard).Where(item => GetLayer(item) == layer))
                    item.Selected = true;
                hcsm.MultiBoard.Focus();
            }
            UpdateSelectionFromBoard();
            UpdateStatistics();
        }
    }
}
