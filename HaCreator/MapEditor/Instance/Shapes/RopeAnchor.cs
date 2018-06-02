/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance.Shapes
{
    public class RopeAnchor : MapleDot, IContainsLayerInfo, ISnappable
    {
        private Rope parentRope;

        public RopeAnchor(Board board, int x, int y, Rope parentRope)
            : base(board, x, y)
        {
            this.parentRope = parentRope;
        }

        public override bool CheckIfLayerSelected(SelectionInfo sel)
        {
            // Ropes have no zM
            return (sel.selectedLayer == -1 || sel.selectedLayer == parentRope.LayerNumber);
        }

        public override XNA.Color Color
        {
            get
            {
                return UserSettings.RopeColor;
            }
        }

        public override XNA.Color InactiveColor
        {
            get { return MultiBoard.RopeInactiveColor; }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Ropes; }
        }

        public int LayerNumber
        {
            get { return parentRope.LayerNumber; }
            set { parentRope.LayerNumber = value; }
        }
        public int PlatformNumber { get { return -1; } set { return; } }

        protected override bool RemoveConnectedLines
        {
            // This should never happen because RemoveItem is overridden to remove through parentRope
            get { throw new NotImplementedException(); }
        }

        public override void RemoveItem(List<UndoRedoAction> undoPipe)
        {
            parentRope.Remove(undoPipe);
        }

        public override void DoSnap()
        {
            // Lookup possible snap to foothold
            FootholdLine closestLine = null;
            double closestDistanceLine = double.MaxValue;
            foreach (FootholdLine fh in Board.BoardItems.FootholdLines)
            {
                if (fh.FirstDot.Selected || fh.SecondDot.Selected)
                    continue;
                if (!fh.IsWall && BetweenOrEquals(X, fh.FirstDot.X, fh.SecondDot.X, (int)UserSettings.SnapDistance) && BetweenOrEquals(Y, fh.FirstDot.Y, fh.SecondDot.Y, (int)UserSettings.SnapDistance))
                {
                    double targetY = fh.CalculateY(X) + 2;
                    double distance = Math.Abs(targetY - Y);
                    if (closestDistanceLine > distance)
                    {
                        closestDistanceLine = distance;
                        closestLine = fh;
                    }
                }
            }

            // Lookup possible snap to rope/ladder
            XNA.Point? closestRopeHint = null;
            double closestDistanceRope = double.MaxValue;
            bool closestIsLadder = false;
            foreach (LayeredItem li in Board.BoardItems.TileObjs)
            {
                if (!(li is ObjectInstance) || li.Selected)
                    continue;
                ObjectInstance objInst = (ObjectInstance)li;
                ObjectInfo objInfo = (ObjectInfo)objInst.BaseInfo;
                if (objInfo.RopeOffsets != null)
                {
                    LookupSnapInOffsetMap(objInst, objInfo.RopeOffsets, false, ref closestRopeHint, ref closestDistanceRope, ref closestIsLadder);
                }
                if (objInfo.LadderOffsets != null)
                {
                    LookupSnapInOffsetMap(objInst, objInfo.LadderOffsets, true, ref closestRopeHint, ref closestDistanceRope, ref closestIsLadder);
                }
            }

            if (closestDistanceRope >= closestDistanceLine && closestLine != null)
            {
                // If foothold is closer, snap to it
                SnapMoveAllMouseBoundItems(new XNA.Point(Parent.X + Parent.BoundItems[this].X, (int)closestLine.CalculateY(X) + 2));
            }
            else if (closestDistanceRope <= closestDistanceLine && closestRopeHint.HasValue)
            {
                // If rope/ladder is closer, snap to it and change our rope/ladder policy, unless it was hard-set by the user
                SnapMoveAllMouseBoundItems(new XNA.Point(closestRopeHint.Value.X, closestRopeHint.Value.Y));
                if (!this.parentRope.ladderSetByUser)
                {
                    this.parentRope.ladder = closestIsLadder;
                }
            }
        }

        private void LookupSnapInOffsetMap(ObjectInstance objInst, List<List<XNA.Point>> offsetMap, bool ladderList, ref XNA.Point? closestRopeHint, ref double closestDistanceRope, ref bool closestIsLadder)
        {
            foreach (List<XNA.Point> offsetList in offsetMap)
            {
                foreach (XNA.Point offset in offsetList)
                {
                    int dx = objInst.X + offset.X - X;
                    int dy = objInst.Y + offset.Y - Y;
                    if (Math.Abs(dx) > UserSettings.SnapDistance || Math.Abs(dy) > UserSettings.SnapDistance)
                        continue;
                    double distance = InputHandler.Distance(dx, dy);
                    if (distance > UserSettings.SnapDistance)
                        continue;
                    if (closestDistanceRope > distance)
                    {
                        closestDistanceRope = distance;
                        closestRopeHint = new XNA.Point(objInst.X + offset.X, objInst.Y + offset.Y);
                        closestIsLadder = ladderList;
                    }
                }
            }
        }

        public Rope ParentRope { get { return parentRope; } }

        public override bool ShouldSelectSerialized
        {
            get
            {
                return true;
            }
        }

        public override List<ISerializableSelector> SelectSerialized(HashSet<ISerializableSelector> serializedItems)
        {
            return new List<ISerializableSelector> { parentRope };
        }
    }
}
