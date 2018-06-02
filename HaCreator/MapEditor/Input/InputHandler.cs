/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XNA = Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using System.Linq;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.Exceptions;
using HaCreator.MapEditor.Info;

namespace HaCreator.MapEditor.Input
{
    public class InputHandler
    {
        private MultiBoard parentBoard;
        private int lastUserInteraction = 0;
        private int lastBackup = 0;

        public void OnUserInteraction()
        {
            lastUserInteraction = Environment.TickCount;
            if (parentBoard != null && parentBoard.SelectedBoard != null)
                parentBoard.SelectedBoard.Dirty = true;
        }

        public void OnBackup()
        {
            lastBackup = Environment.TickCount;
        }

        private bool IsTickCountDiff(ref int source, int ms)
        {
            int diff = Environment.TickCount - source;
            if (diff < 0)
            {
                // This can happen on TickCount overflow
                // We will just reset the timer and return false, to prevent anything special from happening
                source = Environment.TickCount;
                return false;
            }
            return diff >= ms;
        }

        public bool IsUserIdleFor(int ms)
        {
            return IsTickCountDiff(ref lastUserInteraction, ms);
        }

        public bool IsBackupDelayedFor(int ms)
        {
            return IsTickCountDiff(ref lastBackup, ms);
        }

        public InputHandler(MultiBoard parentBoard)
        {
            this.parentBoard = parentBoard;
            parentBoard.LeftMouseDown += new MultiBoard.LeftMouseDownDelegate(parentBoard_LeftMouseDown);
            parentBoard.LeftMouseUp += new MultiBoard.LeftMouseUpDelegate(parentBoard_LeftMouseUp);
            parentBoard.RightMouseClick += new MultiBoard.RightMouseClickDelegate(parentBoard_RightMouseClick);
            parentBoard.MouseDoubleClick += new MultiBoard.MouseDoubleClickDelegate(parentBoard_MouseDoubleClick);
            parentBoard.ShortcutKeyPressed += new MultiBoard.ShortcutKeyPressedDelegate(parentBoard_ShortcutKeyPressed);
            parentBoard.MouseMoved += new MultiBoard.MouseMovedDelegate(parentBoard_MouseMoved);
        }

        public static XNA.Rectangle CreateRectangle(XNA.Point a, XNA.Point b)
        {
            int left, right, top, bottom;
            if (a.X < b.X) { left = a.X; right = b.X; }
            else { left = b.X; right = a.X; }
            if (a.Y < b.Y) { top = a.Y; bottom = b.Y; }
            else { top = b.Y; bottom = a.Y; }
            return new XNA.Rectangle(left, top, right - left, bottom - top);
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(Keys vKey);

        public static bool IsKeyPushedDown(Keys vKey)
        {
            return 0 != (GetAsyncKeyState(vKey) & 0x8000);
        }

        public static double Distance(double x, double y)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }
        
        private void parentBoard_MouseMoved(Board selectedBoard, XNA.Point oldPos, XNA.Point newPos, XNA.Point currPhysicalPos)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (selectedBoard.Mouse.MinimapBrowseOngoing && selectedBoard.Mouse.State == MouseState.Selection)
                {
                    HandleMinimapBrowse(selectedBoard, currPhysicalPos);
                }
                else if (selectedBoard.Mouse.MultiSelectOngoing && (Math.Abs(selectedBoard.Mouse.X - selectedBoard.Mouse.MultiSelectStart.X) > 1 || Math.Abs(selectedBoard.Mouse.Y - selectedBoard.Mouse.MultiSelectStart.Y) > 1))
                {
                    XNA.Rectangle oldRect = CreateRectangle(oldPos, selectedBoard.Mouse.MultiSelectStart);
                    XNA.Rectangle newRect = CreateRectangle(newPos, selectedBoard.Mouse.MultiSelectStart);
                    List<BoardItem> toRemove = new List<BoardItem>();
                    SelectionInfo sel = selectedBoard.GetUserSelectionInfo();
                    foreach (BoardItem item in selectedBoard.BoardItems.Items)
                    {
                        if (MultiBoard.IsItemUnderRectangle(item, newRect) && (sel.editedTypes & item.Type) == item.Type && item.CheckIfLayerSelected(sel))
                            item.Selected = true;
                        else if (item.Selected && MultiBoard.IsItemUnderRectangle(item, oldRect))
                            toRemove.Add(item);
                    }
                    foreach (BoardItem item in toRemove)
                        item.Selected = false;
                    toRemove.Clear();
                }
                else if (selectedBoard.Mouse.SingleSelectStarting && (Distance(newPos.X - selectedBoard.Mouse.SingleSelectStart.X, newPos.Y - selectedBoard.Mouse.SingleSelectStart.Y) > UserSettings.SignificantDistance || IsKeyPushedDown(Keys.Menu)))
                {
                    BindAllSelectedItems(selectedBoard, selectedBoard.Mouse.SingleSelectStart);
                    selectedBoard.Mouse.SingleSelectStarting = false;
                }
                else if (selectedBoard.Mouse.BoundItems.Count > 0)
                {
                    //snapping
                    if (UserSettings.useSnapping && selectedBoard.Mouse.BoundItems.Count != 0 && !IsKeyPushedDown(Keys.Menu))
                    {
                        MouseState state = selectedBoard.Mouse.State;
                        if (state == MouseState.Selection || state == MouseState.StaticObjectAdding || state == MouseState.RandomTiles || state == MouseState.Ropes || state == MouseState.Footholds || state == MouseState.Chairs)
                        {
                            List<BoardItem> items = selectedBoard.Mouse.BoundItems.Keys.ToList();
                            foreach (BoardItem item in items)
                            {
                                if (item is ISnappable)
                                    ((ISnappable)item).DoSnap();
                            }
                        }
                    }
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    // Foothold snap-like behavior
                    selectedBoard.Mouse.DoSnap();
                }

                if ((selectedBoard.Mouse.BoundItems.Count > 0 || selectedBoard.Mouse.MultiSelectOngoing) && selectedBoard.Mouse.State == MouseState.Selection)
                {
                    // auto scrolling
                    // Bind physicalpos to our dxcontainer, to prevent extremely fast scrolling
                    currPhysicalPos = new XNA.Point(Math.Min(Math.Max(currPhysicalPos.X, 0), parentBoard.Width), Math.Min(Math.Max(currPhysicalPos.Y, 0), parentBoard.Height));

                    if (currPhysicalPos.X - UserSettings.ScrollDistance < 0 && oldPos.X > newPos.X) //move to left
                        selectedBoard.hScroll = (int)Math.Max(0, selectedBoard.hScroll - Math.Pow(UserSettings.ScrollBase, (UserSettings.ScrollDistance - currPhysicalPos.X) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor);
                    else if (currPhysicalPos.X + UserSettings.ScrollDistance > parentBoard.Width && oldPos.X < newPos.X) //move to right
                        selectedBoard.hScroll = (int)Math.Min(selectedBoard.hScroll + Math.Pow(UserSettings.ScrollBase, (currPhysicalPos.X - parentBoard.Width + UserSettings.ScrollDistance) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor, parentBoard.maxHScroll);
                    if (currPhysicalPos.Y - UserSettings.ScrollDistance < 0 && oldPos.Y > newPos.Y) //move to top
                        selectedBoard.vScroll = (int)Math.Max(0, selectedBoard.vScroll - Math.Pow(UserSettings.ScrollBase, (UserSettings.ScrollDistance - currPhysicalPos.Y) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor);
                    else if (currPhysicalPos.Y + UserSettings.ScrollDistance > parentBoard.Height && oldPos.Y < newPos.Y) //move to bottom
                        selectedBoard.vScroll = (int)Math.Min(selectedBoard.vScroll + Math.Pow(UserSettings.ScrollBase, (currPhysicalPos.Y - parentBoard.Height + UserSettings.ScrollDistance) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor, parentBoard.maxVScroll);
                }
            }
        }

        private UndoRedoAction CreateItemUndoMoveAction(BoardItem item, XNA.Point posChange)
        {
            if (item is BackgroundInstance)
                return UndoRedoManager.BackgroundMoved((BackgroundInstance)item, new XNA.Point(((BackgroundInstance)item).BaseX + posChange.X, ((BackgroundInstance)item).BaseY + posChange.Y), new XNA.Point(((BackgroundInstance)item).BaseX, ((BackgroundInstance)item).BaseY));
            else
                return UndoRedoManager.ItemMoved(item, new XNA.Point(item.X + posChange.X, item.Y + posChange.Y), new XNA.Point(item.X, item.Y));
        }

        private void parentBoard_ShortcutKeyPressed(Board selectedBoard, bool ctrl, bool shift, bool alt, Keys key)
        {
            lock (parentBoard)
            {
                if (parentBoard == null || parentBoard.SelectedBoard == null)
                    return;
                OnUserInteraction();
                List<UndoRedoAction> actions = new List<UndoRedoAction>();
                if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu /*ALT key*/)
                    return;
                bool clearRedo = true;
                switch (key)
                {
                    case Keys.Left:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                            if (!item.BoundToSelectedItem(selectedBoard))
                            {
                                item.X--;
                                actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(1, 0)));
                            }
                        break;
                    case Keys.Right:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                            if (!item.BoundToSelectedItem(selectedBoard))
                            {
                                item.X++;
                                actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(-1, 0)));
                            }
                        break;
                    case Keys.Up:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                            if (!item.BoundToSelectedItem(selectedBoard))
                            {
                                item.Y--;
                                actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(0, 1)));
                            }
                        break;
                    case Keys.Down:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                            if (!item.BoundToSelectedItem(selectedBoard))
                            {
                                item.Y++;
                                actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(0, -1)));
                            }
                        break;
                    case Keys.Delete:
                        switch (selectedBoard.Mouse.State)
                        {
                            case MouseState.Selection:
                                bool askedVr = false, askedMm = false;
                                List<BoardItem> selectedItems = selectedBoard.SelectedItems.ToList(); // Dupe the selection list
                                foreach (BoardItem item in selectedItems)
                                {
                                    if (item is ToolTipDot || item is MiscDot)
                                        continue;
                                    else if (item is VRDot)
                                    {
                                        if (!askedVr)
                                        {
                                            askedVr = true;
                                            if (MessageBox.Show("This will remove the map's VR. This is not undoable, you must re-add VR from the map's main menu. Continue?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                                            {
                                                selectedBoard.VRRectangle.RemoveItem(null);
                                            }
                                        }
                                    }
                                    else if (item is MinimapDot)
                                    {
                                        if (!askedMm)
                                        {
                                            askedMm = true;
                                            if (MessageBox.Show("This will remove the map's minimap. This is not undoable, you must re-add the minimap from the map's main menu. Continue?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                                            {
                                                selectedBoard.MinimapRectangle.RemoveItem(null);
                                            }
                                        }
                                    }
                                    else
                                        item.RemoveItem(actions);
                                }
                                break;
                            case MouseState.RandomTiles:
                            case MouseState.StaticObjectAdding:
                            case MouseState.Chairs:
                            case MouseState.Ropes:
                                parentBoard.InvokeReturnToSelectionState();
                                break;
                            case MouseState.Footholds:
                                while (selectedBoard.Mouse.connectedLines.Count > 0 && selectedBoard.Mouse.connectedLines[0].FirstDot.connectedLines.Count > 0)
                                    selectedBoard.Mouse.connectedLines[0].FirstDot.connectedLines[0].Remove(false, actions);
                                break;
                        }
                        break;
                    case Keys.F:
                        if (ctrl)
                        {
                            foreach (BoardItem item in selectedBoard.SelectedItems)
                            {
                                if (item is IFlippable)
                                {
                                    ((IFlippable)item).Flip = !((IFlippable)item).Flip;
                                    actions.Add(UndoRedoManager.ItemFlipped((IFlippable)item));
                                }
                            }
                        }
                        break;
                    case Keys.Add:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                        {
                            item.Z += UserSettings.zShift;
                            actions.Add(UndoRedoManager.ItemZChanged(item, item.Z - UserSettings.zShift, item.Z));
                        }
                        selectedBoard.BoardItems.Sort();
                        break;
                    case Keys.Subtract:
                        foreach (BoardItem item in selectedBoard.SelectedItems)
                        {
                            item.Z -= UserSettings.zShift;
                            actions.Add(UndoRedoManager.ItemZChanged(item, item.Z + UserSettings.zShift, item.Z));
                        }
                        selectedBoard.BoardItems.Sort();
                        break;
                    case Keys.A:
                        if (ctrl)
                        {
                            foreach (BoardItem item in selectedBoard.BoardItems.Items)
                            {
                                if ((selectedBoard.EditedTypes & item.Type) == item.Type)
                                {
                                    if (item is LayeredItem)
                                    {
                                        LayeredItem li = (LayeredItem)item;
                                        if (li.CheckIfLayerSelected(selectedBoard.GetUserSelectionInfo()))
                                        {
                                            item.Selected = true;
                                        }
                                    }
                                    else
                                        item.Selected = true;
                                }
                            }
                        }
                        clearRedo = false;
                        break;
                    case Keys.X: // Cut
                        if (ctrl && selectedBoard.Mouse.State == MouseState.Selection)
                        {
                            Clipboard.SetData(SerializationManager.HaClipboardData, 
                                selectedBoard.SerializationManager.SerializeList(selectedBoard.SelectedItems.Cast<ISerializableSelector>()));
                            int selectedItemIndex = 0;
                            while (selectedBoard.SelectedItems.Count > selectedItemIndex)
                            {
                                BoardItem item = selectedBoard.SelectedItems[selectedItemIndex];
                                if (item is ToolTipDot || item is MiscDot || item is VRDot || item is MinimapDot)
                                    selectedItemIndex++;
                                else
                                    item.RemoveItem(actions);
                            }
                            break;
                        }
                        break;
                    case Keys.C: // Copy
                        if (ctrl)
                        {
                            Clipboard.SetData(SerializationManager.HaClipboardData, 
                                selectedBoard.SerializationManager.SerializeList(selectedBoard.SelectedItems.Cast<ISerializableSelector>()));
                        }
                        break;
                    case Keys.V: // Paste
                        if (ctrl && Clipboard.ContainsData(SerializationManager.HaClipboardData))
                        {
                            List<ISerializable> items;
                            try
                            {
                                items = selectedBoard.SerializationManager.DeserializeList((string)Clipboard.GetData(SerializationManager.HaClipboardData));
                            }
                            catch (SerializationException de)
                            {
                                MessageBox.Show(de.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(string.Format("An error occurred: {0}", e.ToString()), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            bool needsLayer = false;
                            
                            // Make sure we dont have any tS conflicts
                            string tS = null;
                            foreach (ISerializable item in items)
                            {
                                if (item is TileInstance)
                                {
                                    TileInstance tile = (TileInstance)item;
                                    string currtS = ((TileInfo)tile.BaseInfo).tS;
                                    if (currtS != tS)
                                    {
                                        if (tS == null)
                                            tS = currtS;
                                        else
                                        {
                                            MessageBox.Show("Clipboard contains two tiles with different tile sets, cannot paste.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            return;
                                        }
                                    }
                                }
                                if (item is IContainsLayerInfo)
                                {
                                    needsLayer = true;
                                }
                            }
                            if (needsLayer && (selectedBoard.SelectedLayerIndex < 0 || selectedBoard.SelectedPlatform < 0))
                            {
                                MessageBox.Show("Layered items in clipboard and no layer/platform selected, cannot paste.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            if (tS != null && selectedBoard.SelectedLayer.tS != null && tS != selectedBoard.SelectedLayer.tS)
                            {
                                MessageBox.Show("Clipboard contains tile in a different set than the current selected layer, cannot paste.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            // Calculate offsetting
                            XNA.Point minPos = new XNA.Point(int.MaxValue, int.MaxValue);
                            XNA.Point maxPos = new XNA.Point(int.MinValue, int.MinValue);
                            foreach (ISerializable item in items)
                            {
                                if (item is BoardItem)
                                {
                                    BoardItem bi = (BoardItem)item;
                                    if (bi.Left < minPos.X)
                                        minPos.X = bi.Left;
                                    if (bi.Top < minPos.Y)
                                        minPos.Y = bi.Top;
                                    if (bi.Right > maxPos.X)
                                        maxPos.X = bi.Right;
                                    if (bi.Bottom > maxPos.Y)
                                        maxPos.Y = bi.Bottom;
                                }
                                else if (item is Rope)
                                {
                                    Rope r = (Rope)item;
                                    int x = r.FirstAnchor.X;
                                    int minY = Math.Min(r.FirstAnchor.Y, r.SecondAnchor.Y);
                                    int maxY = Math.Max(r.FirstAnchor.Y, r.SecondAnchor.Y);
                                    if (x < minPos.X)
                                        minPos.X = x;
                                    if (x > maxPos.X)
                                        maxPos.X = x;
                                    if (minY < minPos.Y)
                                        minPos.Y = minY;
                                    if (maxY > maxPos.Y)
                                        maxPos.Y = maxY;
                                }
                            }
                            XNA.Point center = new XNA.Point((maxPos.X + minPos.X) / 2, (maxPos.Y + minPos.Y) / 2);
                            XNA.Point offset = new XNA.Point(selectedBoard.Mouse.X - center.X, selectedBoard.Mouse.Y - center.Y);

                            // Add the items
                            ClearSelectedItems(selectedBoard);
                            List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                            foreach(ISerializable item in items)
                            {
                                item.AddToBoard(undoPipe);
                                item.PostDeserializationActions(true, offset);
                            }
                            selectedBoard.BoardItems.Sort();
                            selectedBoard.UndoRedoMan.AddUndoBatch(undoPipe);
                        }
                        break;
                    case Keys.Z:
                        if (ctrl && selectedBoard.UndoRedoMan.UndoList.Count > 0) selectedBoard.UndoRedoMan.Undo();
                        clearRedo = false;
                        break;
                    case Keys.Y:
                        if (ctrl && selectedBoard.UndoRedoMan.RedoList.Count > 0) selectedBoard.UndoRedoMan.Redo();
                        clearRedo = false;
                        break;
                    case Keys.S:
                        if (ctrl)
                            parentBoard.OnExportRequested();
                        break;
                    case Keys.O:
                        if (ctrl)
                            parentBoard.OnLoadRequested();
                        break;
                    case Keys.Escape:
                        if (selectedBoard.Mouse.State == MouseState.Selection)
                        {
                            ClearBoundItems(selectedBoard);
                            ClearSelectedItems(selectedBoard);
                            clearRedo = false;
                        }
                        else if (selectedBoard.Mouse.State == MouseState.Footholds)
                            selectedBoard.Mouse.Clear();
                        else
                            parentBoard.InvokeReturnToSelectionState();
                        break;
                    default:
                        clearRedo = false;
                        break;
                    case Keys.W:
                        if (ctrl)
                            parentBoard.OnCloseTabRequested();
                        break;
                    case Keys.Tab:
                        if (ctrl)
                            parentBoard.OnSwitchTabRequested(shift);
                        break;
                }
                if (actions.Count > 0)
                    selectedBoard.UndoRedoMan.AddUndoBatch(actions);
                if (clearRedo)
                    selectedBoard.UndoRedoMan.RedoList.Clear();
            }
        }

        private bool ClickOnMinimap(Board selectedBoard, XNA.Point position)
        {
            if (selectedBoard.MiniMap == null || !UserSettings.useMiniMap) return false;
            return position.X > 0 && position.X < selectedBoard.MinimapArea.Width && position.Y > 0 && position.Y < selectedBoard.MinimapArea.Height;

        }

        private void parentBoard_MouseDoubleClick(Board selectedBoard, BoardItem target, XNA.Point realPosition, XNA.Point virtualPosition)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (ClickOnMinimap(selectedBoard, realPosition)) return;
                if (target != null)
                {
                    ClearSelectedItems(selectedBoard);
                    target.Selected = true;
                    parentBoard.EditInstanceClicked(target);
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    selectedBoard.Mouse.CreateFhAnchor();
                }
            }
        }

        private void parentBoard_RightMouseClick(Board selectedBoard, BoardItem rightClickTarget, XNA.Point realPosition, XNA.Point virtualPosition, MouseState mouseState)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (mouseState == MouseState.Selection)
                {
                    ClearBoundItems(selectedBoard);
                    if (ClickOnMinimap(selectedBoard, realPosition)) return;
                    if (rightClickTarget == null)
                        return;
                    if (!rightClickTarget.Selected)
                        ClearSelectedItems(selectedBoard);
                    rightClickTarget.Selected = true;
                    BoardItemContextMenu bicm = new BoardItemContextMenu(parentBoard, selectedBoard, rightClickTarget);
                    bicm.Menu.Show(parentBoard.PointToScreen(new System.Drawing.Point(realPosition.X, realPosition.Y)));
                }
                else parentBoard.InvokeReturnToSelectionState();
            }
        }

        private void parentBoard_LeftMouseUp(Board selectedBoard, BoardItem target, BoardItem selectedTarget, XNA.Point realPosition, XNA.Point virtualPosition, bool selectedItemHigher)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (selectedBoard.Mouse.State == MouseState.Selection)//handle drag-drop selection end
                {
                    ClearBoundItems(selectedBoard);
                }
                else if (selectedBoard.Mouse.State == MouseState.StaticObjectAdding ||
                    selectedBoard.Mouse.State == MouseState.RandomTiles ||
                    selectedBoard.Mouse.State == MouseState.Chairs ||
                    selectedBoard.Mouse.State == MouseState.Ropes ||
                    selectedBoard.Mouse.State == MouseState.Tooltip ||
                    selectedBoard.Mouse.State == MouseState.Clock) //handle clicks that are meant to add an item to the board
                {
                    selectedBoard.Mouse.PlaceObject();
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    selectedBoard.Mouse.TryConnectFoothold();
                }
            }
        }
        
        private void HandleMinimapBrowse(Board selectedBoard, XNA.Point realPosition)
        {
            int h = realPosition.X * selectedBoard.mag - parentBoard.Width / 2;
            int v = realPosition.Y * selectedBoard.mag - parentBoard.Height / 2;
            if (h < 0) selectedBoard.hScroll = 0;
            else if (h > parentBoard.maxHScroll) selectedBoard.hScroll = parentBoard.maxHScroll;
            else selectedBoard.hScroll = h;
            if (v < 0) selectedBoard.vScroll = 0;
            else if (v > parentBoard.maxVScroll) selectedBoard.vScroll = parentBoard.maxVScroll;
            else selectedBoard.vScroll = v;
        }

        private void parentBoard_LeftMouseDown(Board selectedBoard, BoardItem item, BoardItem selectedItem, XNA.Point realPosition, XNA.Point virtualPosition, bool selectedItemHigher)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (ClickOnMinimap(selectedBoard, realPosition) && selectedBoard.Mouse.State == MouseState.Selection)
                {
                    //ClearSelectedItems(selectedBoard);
                    selectedBoard.Mouse.MinimapBrowseOngoing = true;
                    HandleMinimapBrowse(selectedBoard, realPosition);
                }
                else if (selectedBoard.Mouse.State == MouseState.Selection)
                {
                    //handle drag-drop, multiple selection and all that
                    bool ctrlDown = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    if (item == null && selectedItem == null) //drag-selection is starting
                    {
                        if (!ctrlDown)
                        {
                            ClearSelectedItems(selectedBoard);
                        }
                        selectedBoard.Mouse.MultiSelectOngoing = true;
                        selectedBoard.Mouse.MultiSelectStart = virtualPosition;
                    }
                    else //Single click on item
                    {
                        BoardItem itemToSelect = null;
                        bool itemAlreadySelected = false;

                        if (item == null) // If user didn't click on any non-selected item, we want to keep selectedItem as our bound item
                        {
                            itemToSelect = selectedItem;
                            itemAlreadySelected = true;
                        }
                        else if (selectedItem == null) // We are guaranteed (item != null) at this point, so just select item
                        {
                            itemToSelect = item;
                        }
                        else if (!selectedItemHigher) // item needs to be selected but there is already a selectedItem; only switch selection if the selectedItem is not higher
                        {
                            itemToSelect = item;
                        }
                        else // Otherwise, just mark selectedItem as the item we are selecting
                        {
                            itemToSelect = selectedItem;
                            itemAlreadySelected = true;
                        }

                        if (!itemAlreadySelected && !ctrlDown) // If we are changing selection and ctrl is not down, clear current selected items
                        {
                            ClearSelectedItems(selectedBoard);
                        }
                        if (ctrlDown) // If we are clicking an item and ctrl IS down, we need to toggle its selection
                        {
                            itemToSelect.Selected = !itemToSelect.Selected;
                        }
                        else // Otherwise, mark the item as selected (if it's already selected nothing will happen) and bind it to the mouse to start drag-drop action
                        {
                            itemToSelect.Selected = true;
                            selectedBoard.Mouse.SingleSelectStarting = true;
                            selectedBoard.Mouse.SingleSelectStart = virtualPosition;
                            //BindAllSelectedItems(selectedBoard); // not binding selected items here because we will bind them after significant movement
                        }
                    }
                }
            }
        }

        private void BindAllSelectedItems(Board selectedBoard)
        {
            BindAllSelectedItems(selectedBoard, new XNA.Point(selectedBoard.Mouse.X, selectedBoard.Mouse.Y));
        }

        private void BindAllSelectedItems(Board selectedBoard, XNA.Point mousePosition)
        {
            foreach (BoardItem itemToSelect in selectedBoard.SelectedItems)
            {
                selectedBoard.Mouse.BindItem(itemToSelect, new XNA.Point(itemToSelect.X - mousePosition.X, itemToSelect.Y - mousePosition.Y));
                if (itemToSelect is BackgroundInstance)
                    itemToSelect.moveStartPos = new XNA.Point(((BackgroundInstance)itemToSelect).BaseX, ((BackgroundInstance)itemToSelect).BaseY);
                else
                    itemToSelect.moveStartPos = new XNA.Point(itemToSelect.X, itemToSelect.Y);
            }
        }

        public static void ClearSelectedItems(Board board)
        {
            lock (board.ParentControl)
            {
                while (board.SelectedItems.Count > 0)
                {
                    board.SelectedItems[0].Selected = false;
                }
            }
        }

        public static void ClearBoundItems(Board board)
        {
            lock (board.ParentControl)
            {
                List<UndoRedoAction> undoActions = new List<UndoRedoAction>();
                bool addUndo;
                List<BoardItem> items = board.Mouse.BoundItems.Keys.ToList();
                foreach (BoardItem item in items)
                {
                    addUndo = item.tempParent == null || !(item.tempParent.Parent is Mouse);
                    board.Mouse.ReleaseItem(item);
                    if (addUndo)
                    {
                        if ((item is BackgroundInstance) && (((BackgroundInstance)item).BaseX != item.moveStartPos.X || ((BackgroundInstance)item).BaseY != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.BackgroundMoved((BackgroundInstance)item, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(((BackgroundInstance)item).BaseX, ((BackgroundInstance)item).BaseY)));
                        else if (!(item is BackgroundInstance) && (item.X != item.moveStartPos.X || item.Y != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.ItemMoved(item, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(item.X, item.Y)));
                    }
                }
                if (undoActions.Count > 0)
                    board.UndoRedoMan.AddUndoBatch(undoActions);
            }
        }
    }
}
