/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;

namespace HaCreator.MapEditor.UndoRedo
{
    public class UndoRedoManager
    {
        public List<UndoRedoBatch> UndoList = new List<UndoRedoBatch>();
        public List<UndoRedoBatch> RedoList = new List<UndoRedoBatch>();
        private Board parentBoard;

        public UndoRedoManager(Board parentBoard)
        {
            this.parentBoard = parentBoard;
        }

        public void AddUndoBatch(List<UndoRedoAction> actions)
        {
            if (actions.Count == 0)
                return;
            UndoRedoBatch batch = new UndoRedoBatch() { Actions = actions };
            UndoList.Add(batch);
            RedoList.Clear();
            parentBoard.ParentControl.UndoListChanged();
            parentBoard.ParentControl.RedoListChanged();
        }

        #region Undo Actions Creation
        public static UndoRedoAction ItemAdded(BoardItem item)
        {
            return new UndoRedoAction(item, UndoRedoType.ItemAdded, null, null);
        }

        public static UndoRedoAction ItemDeleted(BoardItem item)
        {
            return new UndoRedoAction(item, UndoRedoType.ItemDeleted, null, null);
        }

        public static UndoRedoAction ItemMoved(BoardItem item, Point oldPos, Point newPos)
        {
            return new UndoRedoAction(item, UndoRedoType.ItemMoved, oldPos, newPos);
        }

        public static UndoRedoAction VRChanged(Rectangle oldVR, Rectangle newVR)
        {
            return new UndoRedoAction(null, UndoRedoType.VRChanged, oldVR, newVR);
        }

        public static UndoRedoAction MapCenterChanged(Point oldCenter, Point newCenter)
        {
            return new UndoRedoAction(null, UndoRedoType.MapCenterChanged, oldCenter, newCenter);
        }

        public static UndoRedoAction ItemFlipped(IFlippable item)
        {
            return new UndoRedoAction((BoardItem)item, UndoRedoType.ItemFlipped, null, null);
        }

        public static UndoRedoAction LineRemoved(MapleLine line, MapleDot a, MapleDot b)
        {
            return new UndoRedoAction(null, UndoRedoType.LineRemoved, a, b, line);
        }

        public static UndoRedoAction LineAdded(MapleLine line, MapleDot a, MapleDot b)
        {
            return new UndoRedoAction(null, UndoRedoType.LineAdded, a, b, line);
        }

        public static UndoRedoAction ToolTipLinked(ToolTipInstance tt, ToolTipChar ttc)
        {
            return new UndoRedoAction(tt, UndoRedoType.ToolTipLinked, ttc, null);
        }

        public static UndoRedoAction ToolTipUnlinked(ToolTipInstance tt, ToolTipChar ttc)
        {
            return new UndoRedoAction(tt, UndoRedoType.ToolTipUnlinked, ttc, null);
        }

        public static UndoRedoAction BackgroundMoved(BackgroundInstance item, Point oldPos, Point newPos)
        {
            return new UndoRedoAction(item, UndoRedoType.BackgroundMoved, oldPos, newPos);
        }

        public static UndoRedoAction ItemsLinked(BoardItem parent, BoardItem child, Point distance)
        {
            return new UndoRedoAction(parent, UndoRedoType.ItemsLinked, child, distance);
        }

        public static UndoRedoAction ItemsUnlinked(BoardItem parent, BoardItem child, Point distance)
        {
            return new UndoRedoAction(parent, UndoRedoType.ItemsUnlinked, child, distance);
        }

        public static UndoRedoAction ItemsLayerChanged(List<IContainsLayerInfo> items, int oldLayerIndex, int newLayerIndex)
        {
            return new UndoRedoAction(null, UndoRedoType.ItemsLayerChanged, oldLayerIndex, newLayerIndex, items);
        }

        public static UndoRedoAction ItemLayerPlatChanged(IContainsLayerInfo item, Tuple<int, int> oldLayerPlat, Tuple<int, int> newLayerPlat)
        {
            return new UndoRedoAction(null, UndoRedoType.ItemLayerPlatChanged, oldLayerPlat, newLayerPlat, item);
        }

        public static UndoRedoAction RopeRemoved(Rope rope)
        {
            return new UndoRedoAction(null, UndoRedoType.RopeRemoved, rope, null);
        }

        public static UndoRedoAction RopeAdded(Rope rope)
        {
            return new UndoRedoAction(null, UndoRedoType.RopeAdded, rope, null);
        }

        public static UndoRedoAction ItemZChanged(BoardItem item, int oldZ, int newZ)
        {
            return new UndoRedoAction(item, UndoRedoType.ItemZChanged, oldZ, newZ);
        }

        public static UndoRedoAction LayerTSChanged(Layer layer, string oldTS, string newTS)
        {
            return new UndoRedoAction(null, UndoRedoType.LayerTSChanged, oldTS, newTS, layer);
        }

        public static UndoRedoAction zMChanged(IContainsLayerInfo target, int oldZM, int newZM)
        {
            return new UndoRedoAction(null, UndoRedoType.zMChanged, oldZM, newZM, target);
        }
        #endregion

        public void Undo()
        {
            lock (parentBoard.ParentControl)
            {
                UndoRedoBatch action = UndoList[UndoList.Count - 1];
                action.UndoRedo(parentBoard);
                action.SwitchActions();
                UndoList.RemoveAt(UndoList.Count - 1);
                RedoList.Add(action);
                parentBoard.ParentControl.UndoListChanged();
                parentBoard.ParentControl.RedoListChanged();
            }
        }

        public void Redo()
        {
            lock (parentBoard.ParentControl)
            {
                UndoRedoBatch action = RedoList[RedoList.Count - 1];
                action.UndoRedo(parentBoard);
                action.SwitchActions();
                RedoList.RemoveAt(RedoList.Count - 1);
                UndoList.Add(action);
                parentBoard.ParentControl.UndoListChanged();
                parentBoard.ParentControl.RedoListChanged();
            }
        }
    }
}
