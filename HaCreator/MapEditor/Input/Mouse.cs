/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Security.Cryptography;
using Xna = Microsoft.Xna.Framework;
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;

namespace HaCreator.MapEditor.Input
{
    public class Mouse : MapleDot //inheriting mapledot to make it easier to attach maplelines to it
    {
        private Bitmap placeholder = new Bitmap(1, 1);
        private Point origin = new Point(0, 0);
        private bool isDown;
        private bool minimapBrowseOngoing;
        private bool multiSelectOngoing;
        private Xna.Point multiSelectStart;
        private bool singleSelectStarting;
        private Xna.Point singleSelectStart;
        private MouseState state;
        private MapleDrawableInfo currAddedInfo;
        private BoardItem currAddedObj;
        private TileInfo[] tileRandomList;

        public Mouse(Board board)
            : base(board, 0, 0)
        {
            IsDown = false;
        }

        public static int NextInt32(int max)
        {
            byte[] bytes = new byte[sizeof(int)];
            RNGCryptoServiceProvider Gen = new RNGCryptoServiceProvider();
            Gen.GetBytes(bytes);
            return Math.Abs(BitConverter.ToInt32(bytes, 0) % max);
        }

        public void PlaceObject()
        {
            lock (Board.ParentControl)
            {
                if (state == MouseState.StaticObjectAdding || state == MouseState.RandomTiles)
                {
                    List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                    currAddedObj.OnItemPlaced(undoPipe);
                    Board.UndoRedoMan.AddUndoBatch(undoPipe);
                    ReleaseItem(currAddedObj);
                    if (currAddedObj is LayeredItem)
                    {
                        int highestZ = 0;
                        foreach (LayeredItem item in Board.BoardItems.TileObjs)
                            if (item.Z > highestZ) highestZ = item.Z;
                        currAddedObj.Z = highestZ;
                        Board.BoardItems.Sort();
                    }
                    if (state == MouseState.StaticObjectAdding)
                        currAddedObj = currAddedInfo.CreateInstance(Board.SelectedLayer, Board, X + currAddedInfo.Origin.X - currAddedInfo.Image.Width / 2, Y + currAddedInfo.Origin.Y - currAddedInfo.Image.Height / 2, 50, false);
                    else
                        currAddedObj = tileRandomList[NextInt32(tileRandomList.Length)].CreateInstance(Board.SelectedLayer, Board, X + currAddedInfo.Origin.X - currAddedInfo.Image.Width / 2, Y + currAddedInfo.Origin.Y - currAddedInfo.Image.Height / 2, 50, false);
                    Board.BoardItems.Add(currAddedObj, false);
                    BindItem(currAddedObj, new Microsoft.Xna.Framework.Point(currAddedInfo.Origin.X - currAddedInfo.Image.Width / 2, currAddedInfo.Origin.Y - currAddedInfo.Image.Height / 2));
                }
                else if (state == MouseState.Chairs)
                {
                    Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(currAddedObj) });
                    ReleaseItem(currAddedObj);
                    currAddedObj = new Chair(Board, X, Y);
                    Board.BoardItems.Add(currAddedObj, false);
                    BindItem(currAddedObj, new Microsoft.Xna.Framework.Point());
                }
                else if (state == MouseState.Ropes)
                {
                    int count = BoundItems.Count;
                    RopeAnchor anchor = (RopeAnchor)BoundItems.Keys.ElementAt(0);
                    ReleaseItem(anchor);
                    if (count == 1)
                    {
                        Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.RopeAdded(anchor.ParentRope) });
                        CreateRope();
                    }
                }
                else if (state == MouseState.Tooltip)
                {
                    int count = BoundItems.Count;
                    ToolTipDot dot = (ToolTipDot)BoundItems.Keys.ElementAt(0);
                    ReleaseItem(dot);
                    if (count == 1)
                    {
                        List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                        dot.ParentTooltip.OnItemPlaced(undoPipe);
                        Board.UndoRedoMan.AddUndoBatch(undoPipe);
                        CreateTooltip();
                    }
                }
                else if (state == MouseState.Clock)
                {
                    int count = BoundItems.Count;
                    List<BoardItem> items = BoundItems.Keys.ToList();
                    Clock clock = null;
                    foreach (BoardItem item in items)
                    {
                        if (item is Clock)
                        {
                            clock = (Clock)item;
                        }
                    }
                    foreach (BoardItem item in items)
                    {
                        ReleaseItem(item);
                    }
                    List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                    clock.OnItemPlaced(undoPipe);
                    Board.UndoRedoMan.AddUndoBatch(undoPipe);
                    CreateClock();
                }
            }
        }

        private void CreateRope()
        {
            lock (Board.ParentControl)
            {
                Rope rope = new Rope(Board, X, Y, Y, false, Board.SelectedLayerIndex, true);
                Board.BoardItems.Ropes.Add(rope);
                BindItem(rope.FirstAnchor, new Xna.Point());
                BindItem(rope.SecondAnchor, new Xna.Point());
            }
        }

        private void CreateTooltip()
        {
            lock (Board.ParentControl)
            {
                ToolTipInstance tt = new ToolTipInstance(Board, new Xna.Rectangle(X, Y, 0, 0), "Title", "Description");
                Board.BoardItems.ToolTips.Add(tt);
                BindItem(tt.PointA, new Xna.Point());
                BindItem(tt.PointC, new Xna.Point());
            }
        }

        private void CreateClock()
        {
            lock (Board.ParentControl)
            {
                Clock clock = new Clock(Board, new Xna.Rectangle(X - 100, Y - 100, 200, 200));
                Board.BoardItems.MiscItems.Add(clock);
                BindItem(clock, new Xna.Point(clock.Width / 2, clock.Height / 2));
                BindItem(clock.PointA, new Xna.Point(-clock.Width / 2, -clock.Height / 2));
                BindItem(clock.PointB, new Xna.Point(clock.Width / 2, -clock.Height / 2));
                BindItem(clock.PointC, new Xna.Point(clock.Width / 2, clock.Height / 2));
                BindItem(clock.PointD, new Xna.Point(-clock.Width / 2, clock.Height / 2));
            }
        }


        public void CreateFhAnchor()
        {
            lock (Board.ParentControl)
            {
                FootholdAnchor fhAnchor = new FootholdAnchor(Board, X, Y, Board.SelectedLayerIndex, Board.SelectedPlatform, true);
                Board.BoardItems.FHAnchors.Add(fhAnchor);
                Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(fhAnchor) });
                if (connectedLines.Count == 0)
                {
                    Board.BoardItems.FootholdLines.Add(new FootholdLine(Board, fhAnchor));
                }
                else
                {
                    connectedLines[0].ConnectSecondDot(fhAnchor);
                    Board.BoardItems.FootholdLines.Add(new FootholdLine(Board, fhAnchor));
                }
            }
        }

        public void TryConnectFoothold()
        {
            lock (Board.ParentControl)
            {
                Xna.Point pos = new Xna.Point(X, Y);
                SelectionInfo sel = board.GetUserSelectionInfo();
                foreach (FootholdAnchor anchor in Board.BoardItems.FHAnchors)
                {
                    if (MultiBoard.IsPointInsideRectangle(pos, anchor.Left, anchor.Top, anchor.Right, anchor.Bottom) && anchor.CheckIfLayerSelected(sel))
                    {
                        if (anchor.connectedLines.Count > 1)
                        {
                            continue;
                        }
                        if (connectedLines.Count > 0) // Are we already holding a foothold?
                        {
                            // We are, so connect the two ends
                            // Check that we are not connecting a foothold to itself, or creating duplicate footholds
                            if (connectedLines[0].FirstDot != anchor && !FootholdLine.Exists(anchor.X, anchor.Y, connectedLines[0].FirstDot.X, connectedLines[0].FirstDot.Y, Board))
                            {
                                Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.LineAdded(connectedLines[0], connectedLines[0].FirstDot, anchor) });
                                connectedLines[0].ConnectSecondDot(anchor);
                                // Now that we finished the previous foothold, create a new one between the anchor and the mouse
                                FootholdLine fh = new FootholdLine(Board, anchor);
                                Board.BoardItems.FootholdLines.Add(fh);
                            }
                        }
                        else // Construct a footholdline between the anchor and the mouse
                        {
                            Board.BoardItems.FootholdLines.Add(new FootholdLine(Board, anchor));
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            lock (Board.ParentControl)
            {
                if (currAddedObj != null)
                {
                    currAddedObj.RemoveItem(null);
                    currAddedObj = null;
                }
                if (state == MouseState.Ropes || state == MouseState.Tooltip)
                {
                    if (state == MouseState.Ropes)
                        ((RopeAnchor)BoundItems.Keys.ElementAt(0)).RemoveItem(null);
                    else
                        ((ToolTipDot)BoundItems.Keys.ElementAt(0)).ParentTooltip.RemoveItem(null);
                }
                else if (state == MouseState.Footholds && connectedLines.Count > 0)
                {
                    FootholdLine fh = (FootholdLine)connectedLines[0];
                    fh.Remove(false, null);
                    Board.BoardItems.FootholdLines.Remove(fh);
                } 
                else if (state == MouseState.Clock)
                {
                    List<BoardItem> items = BoundItems.Keys.ToList();
                    foreach (BoardItem item in items)
                    {
                        item.RemoveItem(null);
                    }
                }
                InputHandler.ClearBoundItems(Board);
                InputHandler.ClearSelectedItems(Board);
                IsDown = false;
            }
        }

        public void SelectionMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                currAddedInfo = null;
                tileRandomList = null;
                state = MouseState.Selection;
            }
        }

        public void SetHeldInfo(MapleDrawableInfo newInfo)
        {
            lock (Board.ParentControl)
            {
                Clear();
                if (newInfo.Image == null) 
                    ((MapleExtractableInfo)newInfo).ParseImage();
                currAddedInfo = newInfo;
                currAddedObj = newInfo.CreateInstance(Board.SelectedLayer, Board, X + currAddedInfo.Origin.X - newInfo.Image.Width / 2, Y + currAddedInfo.Origin.Y - newInfo.Image.Height / 2, 50, false);
                Board.BoardItems.Add(currAddedObj, false);
                BindItem(currAddedObj, new Microsoft.Xna.Framework.Point(newInfo.Origin.X - newInfo.Image.Width / 2, newInfo.Origin.Y - newInfo.Image.Height / 2));
                state = MouseState.StaticObjectAdding;
            }
        }

        public void SetRandomTilesMode(TileInfo[] tileList)
        {
            lock (Board.ParentControl)
            {
                Clear();
                tileRandomList = tileList;
                SetHeldInfo(tileRandomList[NextInt32(tileRandomList.Length)]);
                state = MouseState.RandomTiles;
            }
        }

        public void SetFootholdMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                state = MouseState.Footholds;
            }
        }

        public void SetRopeMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                state = MouseState.Ropes;
                CreateRope();
            }
        }

        public void SetChairMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                currAddedObj = new Chair(Board, X, Y);
                Board.BoardItems.Add(currAddedObj, false);
                BindItem(currAddedObj, new Microsoft.Xna.Framework.Point());
                state = MouseState.Chairs;
            }
        }

        public void SetTooltipMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                state = MouseState.Tooltip;
                CreateTooltip();
            }
        }

        public void SetClockMode()
        {
            lock (Board.ParentControl)
            {
                Clear();
                state = MouseState.Clock;
                CreateClock();
            }
        }

        #region Properties
        public bool IsDown
        {
            get { return isDown; }
            set 
            {
                isDown = value;
                if (!isDown)
                {
                    multiSelectOngoing = false;
                    multiSelectStart = new Xna.Point();
                    minimapBrowseOngoing = false;
                    singleSelectStarting = false;
                    singleSelectStart = new Xna.Point();
                }
            }
        }

        public bool MinimapBrowseOngoing
        {
            get { return minimapBrowseOngoing; }
            set { minimapBrowseOngoing = value; }
        }

        public bool MultiSelectOngoing
        {
            get { return multiSelectOngoing; }
            set { multiSelectOngoing = value; }
        }

        public Xna.Point MultiSelectStart
        {
            get { return multiSelectStart; }
            set { multiSelectStart = value; }
        }

        public bool SingleSelectStarting
        {
            get { return singleSelectStarting; }
            set { singleSelectStarting = value; }
        }

        public Xna.Point SingleSelectStart
        {
            get { return singleSelectStart; }
            set { singleSelectStart = value; }
        }

        public MouseState State
        {
            get { return state; }
        }
        #endregion

        #region Overrides
        protected override bool RemoveConnectedLines
        {
            get { return false; }
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return null; }
        }

        public override void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch sprite, Microsoft.Xna.Framework.Color color, int xShift, int yShift)
        {
        }

        public override System.Drawing.Bitmap Image
        {
            get { return placeholder; }
        }

        public override Point Origin
        {
            get { return origin; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.None; }
        }

        public override Microsoft.Xna.Framework.Color Color
        {
            get { return Microsoft.Xna.Framework.Color.White; }
        }

        public override Microsoft.Xna.Framework.Color InactiveColor
        {
            get { return Microsoft.Xna.Framework.Color.White; }
        }

        public override void BindItem(BoardItem item, Microsoft.Xna.Framework.Point distance)
        {
            lock (Board.ParentControl)
            {
                if (BoundItems.ContainsKey(item)) 
                    return;
                BoundItems[item] = distance;
                item.tempParent = item.Parent;
                item.Parent = this;
            }
        }

        public override void ReleaseItem(BoardItem item)
        {
            lock (Board.ParentControl)
            {
                if (BoundItems.ContainsKey(item))
                {
                    BoundItems.Remove(item);
                    item.Parent = item.tempParent;
                    item.tempParent = null;
                }
            }
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
        }
        #endregion
    }
}
