using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Panel that displays all objects on the active board in a hierarchical TreeView.
    /// Provides bidirectional selection sync, search/filter, jump-to-object, and batch operations.
    /// </summary>
    public partial class ObjectViewerPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private Board _currentBoard;
        private bool _suppressSelectionSync;
        private System.Windows.Forms.Timer _refreshTimer;

        public ObjectViewerPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the panel with the state manager and sets up event subscriptions.
        /// </summary>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            hcsm.SetObjectViewerPanel(this);

            // Subscribe to selection changes
            hcsm.MultiBoard.SelectedItemChanged += OnBoardSelectionChanged;

            // Setup refresh timer for debounced updates
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 200; // 200ms debounce
            _refreshTimer.Tick += (s, e) =>
            {
                _refreshTimer.Stop();
                RefreshTreeView();
            };

            // Initial load
            OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
        }

        /// <summary>
        /// Called when the active board changes (tab switch) or when board content changes.
        /// </summary>
        public void OnBoardChanged(Board newBoard)
        {
            _currentBoard = newBoard;
            RefreshTreeView();
        }

        #region Tree Population

        /// <summary>
        /// Refreshes the tree view with items from the current board.
        /// </summary>
        private void RefreshTreeView()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshTreeView));
                return;
            }

            objectTreeView.BeginUpdate();
            objectTreeView.Nodes.Clear();

            Board board = _currentBoard;
            if (board == null || hcsm == null || hcsm.MultiBoard == null)
            {
                objectTreeView.EndUpdate();
                UpdateStatistics();
                return;
            }

            try
            {
                lock (hcsm.MultiBoard)
                {
                    if (board.BoardItems == null)
                    {
                        objectTreeView.EndUpdate();
                        UpdateStatistics();
                        return;
                    }

                    string searchFilter = searchBox.Text?.ToLowerInvariant() ?? "";
                    ItemTypes visibleTypes = board.VisibleTypes;

                    // Add category nodes for all types
                    AddTilesCategory(board, searchFilter, visibleTypes);
                    AddObjectsCategory(board, searchFilter, visibleTypes);
                    AddBackgroundsCategory(board, searchFilter, visibleTypes);
                    AddFlatCategory("NPCs", board.BoardItems.NPCs.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.NPCs);
                    AddFlatCategory("Mobs", board.BoardItems.Mobs.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.Mobs);
                    AddFlatCategory("Reactors", board.BoardItems.Reactors.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.Reactors);
                    AddFlatCategory("Portals", board.BoardItems.Portals.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.Portals);
                    AddFootholdsCategory(board, searchFilter, visibleTypes);
                    AddRopesCategory(board, searchFilter, visibleTypes);
                    AddFlatCategory("Chairs", board.BoardItems.Chairs.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.Chairs);
                    AddTooltipsCategory(board, searchFilter, visibleTypes);
                    AddFlatCategory("Misc", board.BoardItems.MiscItems.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.Misc);
                    AddFlatCategory("MirrorFieldData", board.BoardItems.MirrorFieldDatas.Cast<BoardItem>(), searchFilter, visibleTypes, ItemTypes.MirrorFieldData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObjectViewerPanel.RefreshTreeView error: {ex.Message}");
            }

            objectTreeView.EndUpdate();
            UpdateStatistics();
            UpdateSelectionFromBoard();
        }

        private void AddTilesCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var tiles = board.BoardItems.TileObjs
                .OfType<TileInstance>()
                .Where(t => MatchesSearch(t, searchFilter))
                .ToList();

            if (tiles.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Tiles) != 0;
            TreeNode categoryNode = new TreeNode($"Tiles ({tiles.Count})");
            categoryNode.Tag = "Tiles";
            ApplyVisibilityStyle(categoryNode, isVisible);

            // Group by layer
            var byLayer = tiles.GroupBy(t => t.Layer?.LayerNumber ?? 0).OrderBy(g => g.Key);
            foreach (var group in byLayer)
            {
                TreeNode layerNode = new TreeNode($"Layer {group.Key} ({group.Count()})");
                layerNode.Tag = $"TileLayer_{group.Key}";
                ApplyVisibilityStyle(layerNode, isVisible);

                foreach (var tile in group.OrderBy(t => t.Z))
                {
                    TreeNode itemNode = CreateItemNode(tile, isVisible);
                    layerNode.Nodes.Add(itemNode);
                }
                categoryNode.Nodes.Add(layerNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddObjectsCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var objects = board.BoardItems.TileObjs
                .OfType<ObjectInstance>()
                .Where(o => MatchesSearch(o, searchFilter))
                .ToList();

            if (objects.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Objects) != 0;
            TreeNode categoryNode = new TreeNode($"Objects ({objects.Count})");
            categoryNode.Tag = "Objects";
            ApplyVisibilityStyle(categoryNode, isVisible);

            // Group by layer
            var byLayer = objects.GroupBy(o => o.Layer?.LayerNumber ?? 0).OrderBy(g => g.Key);
            foreach (var group in byLayer)
            {
                TreeNode layerNode = new TreeNode($"Layer {group.Key} ({group.Count()})");
                layerNode.Tag = $"ObjectLayer_{group.Key}";
                ApplyVisibilityStyle(layerNode, isVisible);

                foreach (var obj in group.OrderBy(o => o.Z))
                {
                    TreeNode itemNode = CreateItemNode(obj, isVisible);
                    layerNode.Nodes.Add(itemNode);
                }
                categoryNode.Nodes.Add(layerNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddBackgroundsCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var backBgs = board.BoardItems.BackBackgrounds
                .Where(b => MatchesSearch(b, searchFilter))
                .ToList();
            var frontBgs = board.BoardItems.FrontBackgrounds
                .Where(b => MatchesSearch(b, searchFilter))
                .ToList();

            int totalCount = backBgs.Count + frontBgs.Count;
            if (totalCount == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Backgrounds) != 0;
            TreeNode categoryNode = new TreeNode($"Backgrounds ({totalCount})");
            categoryNode.Tag = "Backgrounds";
            ApplyVisibilityStyle(categoryNode, isVisible);

            if (backBgs.Count > 0)
            {
                TreeNode backNode = new TreeNode($"Back ({backBgs.Count})");
                backNode.Tag = "BackgroundsBack";
                ApplyVisibilityStyle(backNode, isVisible);

                foreach (var bg in backBgs.OrderBy(b => b.Z))
                {
                    TreeNode itemNode = CreateItemNode(bg, isVisible);
                    backNode.Nodes.Add(itemNode);
                }
                categoryNode.Nodes.Add(backNode);
            }

            if (frontBgs.Count > 0)
            {
                TreeNode frontNode = new TreeNode($"Front ({frontBgs.Count})");
                frontNode.Tag = "BackgroundsFront";
                ApplyVisibilityStyle(frontNode, isVisible);

                foreach (var bg in frontBgs.OrderBy(b => b.Z))
                {
                    TreeNode itemNode = CreateItemNode(bg, isVisible);
                    frontNode.Nodes.Add(itemNode);
                }
                categoryNode.Nodes.Add(frontNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddFootholdsCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var anchors = board.BoardItems.FHAnchors
                .Where(a => MatchesSearch(a, searchFilter))
                .ToList();

            if (anchors.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Footholds) != 0;
            TreeNode categoryNode = new TreeNode($"Footholds ({anchors.Count})");
            categoryNode.Tag = "Footholds";
            ApplyVisibilityStyle(categoryNode, isVisible);

            // Group by layer
            var byLayer = anchors.GroupBy(a => a.LayerNumber).OrderBy(g => g.Key);
            foreach (var group in byLayer)
            {
                TreeNode layerNode = new TreeNode($"Layer {group.Key} ({group.Count()})");
                layerNode.Tag = $"FootholdLayer_{group.Key}";
                ApplyVisibilityStyle(layerNode, isVisible);

                foreach (var anchor in group)
                {
                    TreeNode itemNode = CreateItemNode(anchor, isVisible);
                    layerNode.Nodes.Add(itemNode);
                }
                categoryNode.Nodes.Add(layerNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddRopesCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var anchors = board.BoardItems.RopeAnchors
                .Where(a => MatchesSearch(a, searchFilter))
                .ToList();

            if (anchors.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Ropes) != 0;
            TreeNode categoryNode = new TreeNode($"Ropes/Ladders ({anchors.Count})");
            categoryNode.Tag = "Ropes";
            ApplyVisibilityStyle(categoryNode, isVisible);

            foreach (var anchor in anchors)
            {
                TreeNode itemNode = CreateItemNode(anchor, isVisible);
                categoryNode.Nodes.Add(itemNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddTooltipsCategory(Board board, string searchFilter, ItemTypes visibleTypes)
        {
            var tooltips = board.BoardItems.ToolTips
                .Cast<BoardItem>()
                .Concat(board.BoardItems.CharacterToolTips.Cast<BoardItem>())
                .Where(t => MatchesSearch(t, searchFilter))
                .ToList();

            if (tooltips.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.ToolTips) != 0;
            TreeNode categoryNode = new TreeNode($"Tooltips ({tooltips.Count})");
            categoryNode.Tag = "Tooltips";
            ApplyVisibilityStyle(categoryNode, isVisible);

            foreach (var tooltip in tooltips)
            {
                TreeNode itemNode = CreateItemNode(tooltip, isVisible);
                categoryNode.Nodes.Add(itemNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private void AddFlatCategory(string categoryName, IEnumerable<BoardItem> items, string searchFilter, ItemTypes visibleTypes, ItemTypes itemType)
        {
            var filteredItems = items.Where(i => MatchesSearch(i, searchFilter)).ToList();

            if (filteredItems.Count == 0)
                return;

            bool isVisible = (visibleTypes & itemType) != 0;
            TreeNode categoryNode = new TreeNode($"{categoryName} ({filteredItems.Count})");
            categoryNode.Tag = categoryName;
            ApplyVisibilityStyle(categoryNode, isVisible);

            foreach (var item in filteredItems)
            {
                TreeNode itemNode = CreateItemNode(item, isVisible);
                categoryNode.Nodes.Add(itemNode);
            }

            objectTreeView.Nodes.Add(categoryNode);
        }

        private TreeNode CreateItemNode(BoardItem item, bool isTypeVisible)
        {
            string description = GetShortDescription(item);
            string position = $"@ ({item.X}, {item.Y})";

            TreeNode node = new TreeNode($"{description} {position}");
            node.Tag = item;

            ApplyVisibilityStyle(node, isTypeVisible);

            // Highlight selected items
            if (item.Selected)
            {
                node.BackColor = SystemColors.Highlight;
                node.ForeColor = SystemColors.HighlightText;
            }

            return node;
        }

        private void ApplyVisibilityStyle(TreeNode node, bool isVisible)
        {
            if (!isVisible)
            {
                node.ForeColor = SystemColors.GrayText;
            }
        }

        private string GetShortDescription(BoardItem item)
        {
            if (item is TileInstance tile)
            {
                var info = (HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo;
                return $"{info.tS}\\{info.u}\\{info.no}";
            }
            else if (item is ObjectInstance obj)
            {
                var info = (HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo;
                return $"{info.oS}\\{info.l0}\\{info.l1}\\{info.l2}";
            }
            else if (item is BackgroundInstance bg)
            {
                var info = (HaCreator.MapEditor.Info.BackgroundInfo)bg.BaseInfo;
                return $"{info.bS}\\{info.Type}\\{info.no}";
            }
            else if (item is PortalInstance portal)
            {
                return $"{portal.pn} ({portal.pt})";
            }
            else if (item is MobInstance mob)
            {
                var info = (HaCreator.MapEditor.Info.MobInfo)mob.BaseInfo;
                return $"{info.Name} ({info.ID})";
            }
            else if (item is NpcInstance npc)
            {
                var info = (HaCreator.MapEditor.Info.NpcInfo)npc.BaseInfo;
                return $"{info.StringName} ({info.ID})";
            }
            else if (item is ReactorInstance reactor)
            {
                var info = (HaCreator.MapEditor.Info.ReactorInfo)reactor.BaseInfo;
                return $"Reactor {info.ID}";
            }
            else if (item is FootholdAnchor fh)
            {
                return $"Anchor";
            }
            else if (item is RopeAnchor rope)
            {
                return rope.ParentRope?.ladder == true ? "Ladder" : "Rope";
            }
            else if (item is Chair)
            {
                return "Chair";
            }
            else if (item is ToolTipInstance)
            {
                return "Tooltip";
            }
            else if (item is ToolTipChar)
            {
                return "CharTooltip";
            }
            else if (item is MirrorFieldData mirror)
            {
                return $"Mirror ({mirror.MirrorFieldDataType})";
            }
            else if (item is INamedMisc misc)
            {
                return misc.Name;
            }

            return item.GetType().Name;
        }

        private bool MatchesSearch(BoardItem item, string searchFilter)
        {
            if (string.IsNullOrEmpty(searchFilter))
                return true;

            string desc = HaCreatorStateManager.CreateItemDescription(item).ToLowerInvariant();
            return desc.Contains(searchFilter);
        }

        #endregion

        #region Selection Synchronization

        /// <summary>
        /// Called when board selection changes - syncs tree selection from board.
        /// </summary>
        private void OnBoardSelectionChanged(BoardItem selectedItem)
        {
            if (_suppressSelectionSync)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnBoardSelectionChanged(selectedItem)));
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
            if (_currentBoard == null)
                return;

            // Clear all highlights first
            ClearAllHighlights(objectTreeView.Nodes);

            // Highlight selected items - use ToList() to avoid collection modification exception
            List<BoardItem> selectedItems;
            lock (hcsm.MultiBoard)
            {
                selectedItems = _currentBoard.SelectedItems.ToList();
            }

            foreach (BoardItem item in selectedItems)
            {
                TreeNode node = FindNodeByItem(objectTreeView.Nodes, item);
                if (node != null)
                {
                    node.BackColor = SystemColors.Highlight;
                    node.ForeColor = SystemColors.HighlightText;
                    node.EnsureVisible();
                    objectTreeView.SelectedNode = node;
                }
            }
        }

        private void ClearAllHighlights(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is BoardItem item)
                {
                    // Restore visibility-based styling
                    bool isVisible = (_currentBoard.VisibleTypes & item.Type) != 0;
                    node.BackColor = SystemColors.Window;
                    node.ForeColor = isVisible ? SystemColors.WindowText : SystemColors.GrayText;
                }
                ClearAllHighlights(node.Nodes);
            }
        }

        private TreeNode FindNodeByItem(TreeNodeCollection nodes, BoardItem item)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag == item)
                    return node;

                TreeNode found = FindNodeByItem(node.Nodes, item);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Called when tree selection changes - selects item on board and scrolls to it.
        /// </summary>
        private void ObjectTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_suppressSelectionSync)
                return;

            if (e.Node?.Tag is not BoardItem item)
                return;

            _suppressSelectionSync = true;
            try
            {
                lock (hcsm.MultiBoard)
                {
                    // Clear existing selection unless Ctrl is held
                    if (!IsKeyDown(Keys.ControlKey))
                    {
                        InputHandler.ClearSelectedItems(_currentBoard);
                    }

                    // Select the item on the board
                    item.Selected = true;

                    // Scroll the board to center on the selected item
                    CenterViewOnItem(item);

                    hcsm.MultiBoard.Focus();
                }
                UpdateStatistics();
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        private bool IsKeyDown(Keys key)
        {
            return (Control.ModifierKeys & Keys.Control) == Keys.Control;
        }

        #endregion

        #region Jump to Object

        /// <summary>
        /// Centers the viewport on the specified item.
        /// </summary>
        private void CenterViewOnItem(BoardItem item)
        {
            if (_currentBoard == null || hcsm == null)
                return;

            lock (hcsm.MultiBoard)
            {
                // Get viewport size in virtual (map) coordinates, accounting for zoom
                float zoom = _currentBoard.Zoom;
                int viewW = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Width / zoom);
                int viewH = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Height / zoom);

                // Calculate scroll position to center the item
                // Screen position formula: screenPos = (itemPos + centerPoint - scroll) * zoom
                // To center: we want itemPos + centerPoint - scroll = viewW / 2 (in virtual coords)
                // So: scroll = itemPos + centerPoint - viewW / 2
                int targetHScroll = item.X + _currentBoard.CenterPoint.X - viewW / 2;
                int targetVScroll = item.Y + _currentBoard.CenterPoint.Y - viewH / 2;

                // Clamp to valid scroll range (0 to mapSize)
                targetHScroll = Math.Max(0, targetHScroll);
                targetVScroll = Math.Max(0, targetVScroll);

                _currentBoard.hScroll = targetHScroll;
                _currentBoard.vScroll = targetVScroll;
            }
        }

        private void ObjectTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node?.Tag is BoardItem item)
            {
                CenterViewOnItem(item);
            }
        }

        #endregion

        #region Statistics

        private void UpdateStatistics()
        {
            if (_currentBoard == null)
            {
                lblTotalCount.Text = "Total: 0";
                lblSelectedCount.Text = "| Selected: 0";
                return;
            }

            lock (hcsm.MultiBoard)
            {
                int total = _currentBoard.BoardItems.Count;
                int selected = _currentBoard.SelectedItems.Count;

                lblTotalCount.Text = $"Total: {total}";
                lblSelectedCount.Text = $"| Selected: {selected}";
            }
        }

        #endregion

        #region UI Event Handlers

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            // Debounce the search
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshTreeView();
        }

        private void BtnExpandAll_Click(object sender, EventArgs e)
        {
            objectTreeView.ExpandAll();
        }

        private void BtnCollapseAll_Click(object sender, EventArgs e)
        {
            objectTreeView.CollapseAll();
        }

        private void ObjectTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (objectTreeView.SelectedNode?.Tag is BoardItem item)
                {
                    CenterViewOnItem(item);
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Context Menu

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasItem = objectTreeView.SelectedNode?.Tag is BoardItem;
            bool hasCategory = objectTreeView.SelectedNode?.Tag is string;

            selectOnBoardMenuItem.Enabled = hasItem;
            jumpToObjectMenuItem.Enabled = hasItem;
            editPropertiesMenuItem.Enabled = hasItem;
            deleteMenuItem.Enabled = hasItem || (_currentBoard?.SelectedItems.Count > 0);
            selectAllOfTypeMenuItem.Enabled = hasCategory || hasItem;
            selectAllInLayerMenuItem.Enabled = objectTreeView.SelectedNode?.Tag is string tag && tag.Contains("Layer");
        }

        private void SelectOnBoardMenuItem_Click(object sender, EventArgs e)
        {
            if (objectTreeView.SelectedNode?.Tag is BoardItem item)
            {
                lock (hcsm.MultiBoard)
                {
                    InputHandler.ClearSelectedItems(_currentBoard);
                    item.Selected = true;
                    hcsm.MultiBoard.Focus();
                }
            }
        }

        private void JumpToObjectMenuItem_Click(object sender, EventArgs e)
        {
            if (objectTreeView.SelectedNode?.Tag is BoardItem item)
            {
                CenterViewOnItem(item);
            }
        }

        private void EditPropertiesMenuItem_Click(object sender, EventArgs e)
        {
            if (objectTreeView.SelectedNode?.Tag is BoardItem item)
            {
                // Use the existing edit mechanism
                hcsm.MultiBoard.EditInstanceClicked(item);
            }
        }

        private void DeleteMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedItems();
        }

        private void DeleteSelectedItems()
        {
            if (_currentBoard == null)
                return;

            lock (hcsm.MultiBoard)
            {
                List<BoardItem> itemsToDelete = _currentBoard.SelectedItems.ToList();
                if (itemsToDelete.Count == 0 && objectTreeView.SelectedNode?.Tag is BoardItem singleItem)
                {
                    itemsToDelete.Add(singleItem);
                }

                if (itemsToDelete.Count == 0)
                    return;

                List<UndoRedoAction> actions = new List<UndoRedoAction>();

                foreach (BoardItem item in itemsToDelete)
                {
                    // Skip special items that require confirmation
                    if (item is ToolTipDot || item is MiscDot || item is VRDot || item is MinimapDot)
                        continue;

                    item.RemoveItem(actions);
                }

                if (actions.Count > 0)
                {
                    _currentBoard.UndoRedoMan.AddUndoBatch(actions);
                }
            }

            RefreshTreeView();
        }

        private void SelectAllOfTypeMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = objectTreeView.SelectedNode;
            if (selectedNode == null)
                return;

            // Find the category node
            TreeNode categoryNode = selectedNode;
            while (categoryNode.Parent != null && categoryNode.Parent.Tag is string)
            {
                categoryNode = categoryNode.Parent;
            }
            if (categoryNode.Parent != null)
            {
                categoryNode = categoryNode.Parent;
            }

            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(_currentBoard);
                SelectAllItemsInNode(categoryNode);
                hcsm.MultiBoard.Focus();
            }

            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private void SelectAllInLayerMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = objectTreeView.SelectedNode;
            if (selectedNode?.Tag is not string tag || !tag.Contains("Layer"))
                return;

            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(_currentBoard);
                SelectAllItemsInNode(selectedNode);
                hcsm.MultiBoard.Focus();
            }

            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private void SelectAllItemsInNode(TreeNode node)
        {
            if (node.Tag is BoardItem item)
            {
                item.Selected = true;
            }

            foreach (TreeNode child in node.Nodes)
            {
                SelectAllItemsInNode(child);
            }
        }

        #endregion
    }
}
