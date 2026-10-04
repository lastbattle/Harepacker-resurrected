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
        private List<UndoRedoBatch> children;

        internal static UndoRedoBatch Combine(IEnumerable<UndoRedoBatch> chronologicalBatches)
        {
            var result = new UndoRedoBatch { children = chronologicalBatches.Reverse().ToList() };
            result.Actions = result.children.SelectMany(batch => batch.Actions).ToList();
            return result;
        }

        public void UndoRedo(Board board)
        {
            HashSet<int> layersToRecheck = new HashSet<int>();
            Apply(layersToRecheck);
            layersToRecheck.ToList().ForEach(x => board.Layers[x].RecheckTileSet());
        }

        private void Apply(HashSet<int> layersToRecheck)
        {
            if (children != null)
                foreach (var child in children) child.Apply(layersToRecheck);
            else
                foreach (var action in Actions) action.UndoRedo(layersToRecheck);
        }

        public void SwitchActions()
        {
            if (children == null)
                foreach (UndoRedoAction action in Actions) action.SwitchAction();
            else
            {
                // Undo reverses operations; redo must replay them chronologically.
                // Keep each leaf batch's established internal geometry ordering intact.
                foreach (var child in children) child.SwitchActions();
                children.Reverse();
                Actions = children.SelectMany(batch => batch.Actions).ToList();
            }
        }
    }
}
