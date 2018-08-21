/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HaRepacker.GUI.Panels;

namespace HaRepacker
{
    public class UndoRedoManager
    {
        public List<UndoRedoBatch> UndoList = new List<UndoRedoBatch>();
        public List<UndoRedoBatch> RedoList = new List<UndoRedoBatch>();
        private MainPanel parentPanel;

        public UndoRedoManager(MainPanel parentPanel)
        {
            this.parentPanel = parentPanel;
        }

        public void AddUndoBatch(List<UndoRedoAction> actions)
        {
            UndoRedoBatch batch = new UndoRedoBatch() { Actions = actions };
            UndoList.Add(batch);
            RedoList.Clear();
        }

        #region Undo Actions Creation
        public static UndoRedoAction ObjectAdded(WzNode parent, WzNode item)
        {
            return new UndoRedoAction(item, parent, UndoRedoType.ObjectAdded);
        }

        public static UndoRedoAction ObjectRemoved(WzNode parent, WzNode item)
        {
            return new UndoRedoAction(item, parent, UndoRedoType.ObjectRemoved);
        }

        public static UndoRedoAction ObjectRenamed(WzNode parent, WzNode item)
        {
            return new UndoRedoAction(item, parent, UndoRedoType.ObjectRemoved);
        }
        #endregion

        public void Undo()
        {
            UndoRedoBatch action = UndoList[UndoList.Count - 1];
            action.UndoRedo();
            action.SwitchActions();
            UndoList.RemoveAt(UndoList.Count - 1);
            RedoList.Add(action);
        }

        public void Redo()
        {
            UndoRedoBatch action = RedoList[RedoList.Count - 1];
            action.UndoRedo();
            action.SwitchActions();
            RedoList.RemoveAt(RedoList.Count - 1);
            UndoList.Add(action);
        }
    }

    public class UndoRedoBatch
    {
        public List<UndoRedoAction> Actions = new List<UndoRedoAction>();

        public void UndoRedo()
        {
            foreach (UndoRedoAction action in Actions) action.UndoRedo();
        }

        public void SwitchActions()
        {
            foreach (UndoRedoAction action in Actions) action.SwitchAction();
        }
    }

    public class UndoRedoAction
    {
        private WzNode item;
        private WzNode parent;
        private UndoRedoType type;

        public UndoRedoAction(WzNode item, WzNode parent, UndoRedoType type)
        {
            this.item = item;
            this.parent = parent;
            this.type = type;
        }

        public void UndoRedo()
        {
            switch (type)
            {
                case UndoRedoType.ObjectAdded:
                    item.Delete();
                    break;
                case UndoRedoType.ObjectRemoved:
                    parent.AddNode(item);
                    break;
            }
        }


        public unsafe void SwitchAction()
        {
            switch (type)
            {
                case UndoRedoType.ObjectAdded:
                    type = UndoRedoType.ObjectRemoved;
                    break;
                case UndoRedoType.ObjectRemoved:
                    type = UndoRedoType.ObjectAdded;
                    break;

            }
        }
    }

    public enum UndoRedoType
    {
        ObjectAdded,
        ObjectRemoved,
        ObjectRenamed,
        ObjectChanged
    }
}
