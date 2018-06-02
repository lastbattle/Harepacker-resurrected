/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class TooltipInstanceEditor : EditorBase
    {
        public ToolTipInstance item;

        public TooltipInstanceEditor(ToolTipInstance item)
        {
            InitializeComponent();
            this.item = item;
            xInput.Value = item.X;
            yInput.Value = item.Y;
            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item, "\r\n");

            if (item.Title != null)
            {
                useTitleBox.Checked = true;
                titleBox.Text = item.Title;
            }
            if (item.Desc != null)
            {
                useDescBox.Checked = true;
                descBox.Text = item.Desc;
            }
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

                item.Title = useTitleBox.Checked ? titleBox.Text : null;
                item.Desc = useDescBox.Checked ? descBox.Text : null;
            }
            Close();
        }

        private void useTitleBox_CheckedChanged(object sender, EventArgs e)
        {
            titleBox.Enabled = useTitleBox.Checked;
        }

        private void useDescBox_CheckedChanged(object sender, EventArgs e)
        {
            descBox.Enabled = useDescBox.Checked;
        }
    }
}
