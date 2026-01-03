using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class MassZmEditor : EditorBase
    {
        public IContainsLayerInfo[] items;
        private Board board;

        public MassZmEditor(IContainsLayerInfo[] items, Board board, int zm)
        {
            InitializeComponent();
            this.items = items;
            this.board = board;
            zmInput.Value = zm;
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            lock (board.ParentControl)
            {
                int newZM = (int)zmInput.Value;
                List<UndoRedoAction> actions = new List<UndoRedoAction>();
                foreach (IContainsLayerInfo item in items)
                {
                    actions.Add(UndoRedoManager.zMChanged(item, item.PlatformNumber, newZM));
                    item.PlatformNumber = newZM;
                }
                if (actions.Count > 0)
                    board.UndoRedoMan.AddUndoBatch(actions);
            }
            Close();
        }
    }
}
