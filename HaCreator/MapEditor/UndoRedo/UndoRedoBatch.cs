/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.UndoRedo
{
    public class UndoRedoBatch
    {
        public List<UndoRedoAction> Actions = new List<UndoRedoAction>();

        public void UndoRedo(Board board)
        {
            HashSet<int> layersToRecheck = new HashSet<int>();
            foreach (UndoRedoAction action in Actions)
                action.UndoRedo(layersToRecheck);
            layersToRecheck.ToList().ForEach(x => board.Layers[x].RecheckTileSet());
        }

        public void SwitchActions()
        {
            foreach (UndoRedoAction action in Actions) action.SwitchAction();
        }
    }
}
