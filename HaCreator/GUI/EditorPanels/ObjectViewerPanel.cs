using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HaCreator.GUI.EditorPanels
{
    public sealed class ObjectHierarchyNode : INotifyPropertyChanged
    {
        private bool isBoardSelected;
        private bool isExpanded;
        private bool isSelected;

        public ObjectHierarchyNode(string label, object tag, bool isVisible, ObjectHierarchyNode parent = null)
        {
            Label = label;
            Tag = tag;
            IsVisible = isVisible;
            Parent = parent;
        }

        public string Label { get; }
        public object Tag { get; }
        public bool IsVisible { get; }
        public ObjectHierarchyNode Parent { get; }
        public ObservableCollection<ObjectHierarchyNode> Children { get; } = new();

        public bool IsBoardSelected
        {
            get => isBoardSelected;
            set => SetField(ref isBoardSelected, value);
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set => SetField(ref isExpanded, value);
        }

        public bool IsSelected
        {
            get => isSelected;
            set => SetField(ref isSelected, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField(ref bool field, bool value, [CallerMemberName] string propertyName = null)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class ObjectViewerPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private Board currentBoard;
        private bool suppressSelectionSync;
        private readonly DispatcherTimer refreshTimer;
        private readonly ContextMenu contextMenu = new();
        private ObjectHierarchyNode contextNode;

        public ObjectViewerPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            DataContext = this;

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            refreshTimer.Tick += (_, _) =>
            {
                refreshTimer.Stop();
                RefreshTreeView();
            };
            BuildContextMenu();
            objectTreeView.ContextMenu = contextMenu;
        }

        public ObservableCollection<ObjectHierarchyNode> RootNodes { get; } = new();

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetObjectViewerPanel(this);
            hcsm.MultiBoard.SelectedItemChanged += OnBoardSelectionChanged;
            OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
        }

        public void OnBoardChanged(Board newBoard)
        {
            currentBoard = newBoard;
            RefreshTreeView();
        }

        private void BuildContextMenu()
        {
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectOnBoard", "Select on board"), SelectOnBoard_Click));
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_JumpToObject", "Jump to object"), JumpToObject_Click));
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_EditProperties", "Edit properties..."), EditProperties_Click));
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_Delete", "Delete"), Delete_Click));
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectAllOfType", "Select all of type"), SelectAllOfType_Click));
            contextMenu.Items.Add(CreateMenuItem(EditorPanelLocalizer.Text("Menu_SelectAllInLayer", "Select all in layer"), SelectAllInLayer_Click));
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            MenuItem item = new() { Header = header };
            item.Click += handler;
            return item;
        }

        private void RefreshTreeView()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshTreeView));
                return;
            }

            RootNodes.Clear();
            if (currentBoard?.BoardItems == null || hcsm?.MultiBoard == null)
            {
                UpdateStatistics();
                return;
            }

            try
            {
                lock (hcsm.MultiBoard)
                {
                    string filter = searchBox.Text?.Trim() ?? string.Empty;
                    ItemTypes visibleTypes = currentBoard.VisibleTypes;
                    AddLayeredCategory("Tiles", "TileLayer", currentBoard.BoardItems.TileObjs.OfType<TileInstance>(), filter,
                        visibleTypes, ItemTypes.Tiles, item => item.Layer?.LayerNumber ?? 0, item => item.Z);
                    AddLayeredCategory("Objects", "ObjectLayer", currentBoard.BoardItems.TileObjs.OfType<ObjectInstance>(), filter,
                        visibleTypes, ItemTypes.Objects, item => item.Layer?.LayerNumber ?? 0, item => item.Z);
                    AddBackgroundsCategory(filter, visibleTypes);
                    AddFlatCategory("NPCs", currentBoard.BoardItems.NPCs.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.NPCs);
                    AddFlatCategory("Mobs", currentBoard.BoardItems.Mobs.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Mobs);
                    AddFlatCategory("Reactors", currentBoard.BoardItems.Reactors.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Reactors);
                    AddFlatCategory("Portals", currentBoard.BoardItems.Portals.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Portals);
                    AddLayeredCategory("Footholds", "FootholdLayer", currentBoard.BoardItems.FHAnchors, filter,
                        visibleTypes, ItemTypes.Footholds, item => item.LayerNumber, _ => 0);
                    AddFlatCategory("Ropes/Ladders", currentBoard.BoardItems.RopeAnchors.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Ropes);
                    AddFlatCategory("Chairs", currentBoard.BoardItems.Chairs.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Chairs);
                    AddFlatCategory("Tooltips", currentBoard.BoardItems.ToolTips.Cast<BoardItem>()
                        .Concat(currentBoard.BoardItems.CharacterToolTips.Cast<BoardItem>()), filter, visibleTypes, ItemTypes.ToolTips);
                    AddFlatCategory("Misc", currentBoard.BoardItems.MiscItems.Cast<BoardItem>(), filter, visibleTypes, ItemTypes.Misc);
                    AddFlatCategory("MirrorFieldData", currentBoard.BoardItems.MirrorFieldDatas.Cast<BoardItem>(), filter,
                        visibleTypes, ItemTypes.MirrorFieldData);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"ObjectViewerPanel.RefreshTreeView error: {exception.Message}");
            }

            UpdateSelectionFromBoard();
            UpdateStatistics();
        }

        private void AddLayeredCategory<T>(string categoryName, string layerKey, IEnumerable<T> source, string filter,
            ItemTypes visibleTypes, ItemTypes itemType, Func<T, int> getLayer, Func<T, int> getZ) where T : BoardItem
        {
            List<T> items = source.Where(item => MatchesSearch(item, filter)).ToList();
            if (items.Count == 0)
                return;

            bool isVisible = (visibleTypes & itemType) != 0;
            ObjectHierarchyNode category = new($"{categoryName} ({items.Count})", categoryName, isVisible);
            foreach (IGrouping<int, T> group in items.GroupBy(getLayer).OrderBy(group => group.Key))
            {
                ObjectHierarchyNode layer = new($"Layer {group.Key} ({group.Count()})", $"{layerKey}_{group.Key}", isVisible, category);
                foreach (T item in group.OrderBy(getZ))
                    layer.Children.Add(CreateItemNode(item, isVisible, layer));
                category.Children.Add(layer);
            }
            RootNodes.Add(category);
        }

        private void AddBackgroundsCategory(string filter, ItemTypes visibleTypes)
        {
            List<BackgroundInstance> back = currentBoard.BoardItems.BackBackgrounds.Where(item => MatchesSearch(item, filter)).ToList();
            List<BackgroundInstance> front = currentBoard.BoardItems.FrontBackgrounds.Where(item => MatchesSearch(item, filter)).ToList();
            if (back.Count + front.Count == 0)
                return;

            bool isVisible = (visibleTypes & ItemTypes.Backgrounds) != 0;
            ObjectHierarchyNode category = new($"Backgrounds ({back.Count + front.Count})", "Backgrounds", isVisible);
            AddBackgroundGroup(category, "Back", back, isVisible);
            AddBackgroundGroup(category, "Front", front, isVisible);
            RootNodes.Add(category);
        }

        private static void AddBackgroundGroup(ObjectHierarchyNode category, string name, List<BackgroundInstance> items, bool isVisible)
        {
            if (items.Count == 0)
                return;
            ObjectHierarchyNode group = new($"{name} ({items.Count})", $"Backgrounds{name}", isVisible, category);
            foreach (BackgroundInstance item in items.OrderBy(item => item.Z))
                group.Children.Add(CreateItemNode(item, isVisible, group));
            category.Children.Add(group);
        }

        private void AddFlatCategory(string categoryName, IEnumerable<BoardItem> source, string filter,
            ItemTypes visibleTypes, ItemTypes itemType)
        {
            List<BoardItem> items = source.Where(item => MatchesSearch(item, filter)).ToList();
            if (items.Count == 0)
                return;
            bool isVisible = (visibleTypes & itemType) != 0;
            ObjectHierarchyNode category = new($"{categoryName} ({items.Count})", categoryName, isVisible);
            foreach (BoardItem item in items)
                category.Children.Add(CreateItemNode(item, isVisible, category));
            RootNodes.Add(category);
        }

        private static ObjectHierarchyNode CreateItemNode(BoardItem item, bool isVisible, ObjectHierarchyNode parent)
            => new($"{GetShortDescription(item)} @ ({item.X}, {item.Y})", item, isVisible, parent);

        private static bool MatchesSearch(BoardItem item, string filter) =>
            string.IsNullOrEmpty(filter) || HaCreatorStateManager.CreateItemDescription(item).Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static string GetShortDescription(BoardItem item) => item switch
        {
            TileInstance tile => $"{((HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo).tS}\\{((HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo).u}\\{((HaCreator.MapEditor.Info.TileInfo)tile.BaseInfo).no}",
            ObjectInstance obj => $"{((HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo).oS}\\{((HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo).l0}\\{((HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo).l1}\\{((HaCreator.MapEditor.Info.ObjectInfo)obj.BaseInfo).l2}",
            BackgroundInstance background => $"{((HaCreator.MapEditor.Info.BackgroundInfo)background.BaseInfo).bS}\\{((HaCreator.MapEditor.Info.BackgroundInfo)background.BaseInfo).Type}\\{((HaCreator.MapEditor.Info.BackgroundInfo)background.BaseInfo).no}",
            PortalInstance portal => $"{portal.pn} ({portal.pt})",
            MobInstance mob => $"{((HaCreator.MapEditor.Info.MobInfo)mob.BaseInfo).Name} ({((HaCreator.MapEditor.Info.MobInfo)mob.BaseInfo).ID})",
            NpcInstance npc => $"{((HaCreator.MapEditor.Info.NpcInfo)npc.BaseInfo).StringName} ({((HaCreator.MapEditor.Info.NpcInfo)npc.BaseInfo).ID})",
            ReactorInstance reactor => $"Reactor {((HaCreator.MapEditor.Info.ReactorInfo)reactor.BaseInfo).ID}",
            FootholdAnchor => "Anchor",
            RopeAnchor rope => rope.ParentRope?.ladder == true ? "Ladder" : "Rope",
            Chair => "Chair",
            ToolTipInstance => "Tooltip",
            ToolTipChar => "CharTooltip",
            MirrorFieldData mirror => $"Mirror ({mirror.MirrorFieldDataType})",
            INamedMisc misc => misc.Name,
            _ => item.GetType().Name
        };

        private void OnBoardSelectionChanged(BoardItem selectedItem)
        {
            if (suppressSelectionSync)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnBoardSelectionChanged(selectedItem)));
                return;
            }
            suppressSelectionSync = true;
            try
            {
                UpdateSelectionFromBoard();
                UpdateStatistics();
            }
            finally
            {
                suppressSelectionSync = false;
            }
        }

        private void UpdateSelectionFromBoard()
        {
            if (currentBoard == null || hcsm == null)
                return;
            HashSet<BoardItem> selected;
            lock (hcsm.MultiBoard)
                selected = new HashSet<BoardItem>(currentBoard.SelectedItems);

            ObjectHierarchyNode nodeToReveal = null;
            foreach (ObjectHierarchyNode node in EnumerateNodes(RootNodes))
            {
                node.IsBoardSelected = node.Tag is BoardItem item && selected.Contains(item);
                node.IsSelected = false;
                if (node.IsBoardSelected)
                    nodeToReveal = node;
            }
            if (nodeToReveal != null)
            {
                for (ObjectHierarchyNode parent = nodeToReveal.Parent; parent != null; parent = parent.Parent)
                    parent.IsExpanded = true;
                nodeToReveal.IsSelected = true;
            }
        }

        private static IEnumerable<ObjectHierarchyNode> EnumerateNodes(IEnumerable<ObjectHierarchyNode> nodes)
        {
            foreach (ObjectHierarchyNode node in nodes)
            {
                yield return node;
                foreach (ObjectHierarchyNode child in EnumerateNodes(node.Children))
                    yield return child;
            }
        }

        private void ObjectTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (suppressSelectionSync || e.NewValue is not ObjectHierarchyNode { Tag: BoardItem item })
                return;
            suppressSelectionSync = true;
            try
            {
                lock (hcsm.MultiBoard)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                        InputHandler.ClearSelectedItems(currentBoard);
                    item.Selected = true;
                    CenterViewOnItem(item);
                    hcsm.MultiBoard.Focus();
                }
                UpdateSelectionFromBoard();
                UpdateStatistics();
            }
            finally
            {
                suppressSelectionSync = false;
            }
        }

        private void ObjectTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (objectTreeView.SelectedItem is ObjectHierarchyNode { Tag: BoardItem item })
                CenterViewOnItem(item);
        }

        private void ObjectTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject element = e.OriginalSource as DependencyObject;
            while (element != null && element is not TreeViewItem)
                element = VisualTreeHelper.GetParent(element);
            if (element is TreeViewItem treeItem && treeItem.DataContext is ObjectHierarchyNode node)
            {
                treeItem.IsSelected = true;
                contextNode = node;
            }
        }

        private void CenterViewOnItem(BoardItem item)
        {
            if (currentBoard == null || hcsm == null)
                return;
            lock (hcsm.MultiBoard)
            {
                float zoom = currentBoard.Zoom;
                int viewWidth = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Width / zoom);
                int viewHeight = (int)(hcsm.MultiBoard.CurrentDXWindowSize.Height / zoom);
                currentBoard.hScroll = Math.Max(0, item.X + currentBoard.CenterPoint.X - viewWidth / 2);
                currentBoard.vScroll = Math.Max(0, item.Y + currentBoard.CenterPoint.Y - viewHeight / 2);
            }
        }

        private void UpdateStatistics()
        {
            if (currentBoard == null || hcsm == null)
            {
                totalCountText.Text = EditorPanelLocalizer.Format("Format_Total", 0);
                selectedCountText.Text = EditorPanelLocalizer.Format("Format_Selected", 0);
                return;
            }
            lock (hcsm.MultiBoard)
            {
                totalCountText.Text = EditorPanelLocalizer.Format("Format_Total", currentBoard.BoardItems.Count);
                selectedCountText.Text = EditorPanelLocalizer.Format("Format_Selected", currentBoard.SelectedItems.Count);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshTreeView();

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => SetExpanded(RootNodes, true);

        private void CollapseAll_Click(object sender, RoutedEventArgs e) => SetExpanded(RootNodes, false);

        private static void SetExpanded(IEnumerable<ObjectHierarchyNode> nodes, bool value)
        {
            foreach (ObjectHierarchyNode node in nodes)
            {
                node.IsExpanded = value;
                SetExpanded(node.Children, value);
            }
        }

        private void Viewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && objectTreeView.SelectedItem is ObjectHierarchyNode { Tag: BoardItem item })
            {
                CenterViewOnItem(item);
                e.Handled = true;
            }
        }

        private BoardItem ContextBoardItem => contextNode?.Tag as BoardItem;

        private void SelectOnBoard_Click(object sender, RoutedEventArgs e)
        {
            if (ContextBoardItem is not BoardItem item)
                return;
            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(currentBoard);
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
            if (currentBoard == null)
                return;
            lock (hcsm.MultiBoard)
            {
                List<BoardItem> items = currentBoard.SelectedItems.ToList();
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
                    currentBoard.UndoRedoMan.AddUndoBatch(actions);
            }
            RefreshTreeView();
        }

        private void SelectAllOfType_Click(object sender, RoutedEventArgs e)
        {
            if (contextNode == null)
                return;
            ObjectHierarchyNode category = contextNode;
            while (category.Parent != null)
                category = category.Parent;
            SelectItemsInNode(category);
        }

        private void SelectAllInLayer_Click(object sender, RoutedEventArgs e)
        {
            ObjectHierarchyNode layer = contextNode;
            while (layer != null && (layer.Tag is not string value || !value.Contains("Layer", StringComparison.Ordinal)))
                layer = layer.Parent;
            if (layer != null)
                SelectItemsInNode(layer);
        }

        private void SelectItemsInNode(ObjectHierarchyNode node)
        {
            lock (hcsm.MultiBoard)
            {
                InputHandler.ClearSelectedItems(currentBoard);
                foreach (BoardItem item in EnumerateNodes(new[] { node }).Select(treeNode => treeNode.Tag).OfType<BoardItem>())
                    item.Selected = true;
                hcsm.MultiBoard.Focus();
            }
            UpdateSelectionFromBoard();
            UpdateStatistics();
        }
    }
}
