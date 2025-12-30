using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class RopeInstanceEditor : EditorBase
    {
        public RopeAnchor item;

        public RopeInstanceEditor(RopeAnchor item)
        {
            InitializeComponent();
            this.item = item;
            xInput.Value = item.X;
            yInput.Value = item.Y;
            ufBox.Checked = item.ParentRope.uf;
            if (item.ParentRope.ladder)
            {
                ladderBox.Checked = true;
            }
            else
            {
                ladderBox.Checked = false;
            }
            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item);
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            lock (item.Board.ParentControl)
            {
                List<UndoRedoAction> actions = new List<UndoRedoAction>();
                if (xInput.Value != item.X || yInput.Value != item.Y)
                {
                    actions.Add(UndoRedoManager.ItemMoved(item, new Microsoft.Xna.Framework.Point(item.X, item.Y), new Microsoft.Xna.Framework.Point((int)xInput.Value, (int)yInput.Value)));
                    item.Move((int)xInput.Value, (int)yInput.Value);
                }
                if (actions.Count > 0)
                    item.Board.UndoRedoMan.AddUndoBatch(actions);
                item.ParentRope.uf = ufBox.Checked;
                if (item.ParentRope.ladder != ladderBox.Checked)
                {
                    item.ParentRope.OnUserTouchedLadder();
                    item.ParentRope.ladder = ladderBox.Checked;
                }
            }
            Close();
        }
    }
}
