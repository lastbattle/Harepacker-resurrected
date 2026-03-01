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
