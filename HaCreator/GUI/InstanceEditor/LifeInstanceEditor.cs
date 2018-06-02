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
using System.Windows.Forms;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LifeInstanceEditor : EditorBase
    {
        public LifeInstance item;

        public LifeInstanceEditor(LifeInstance item)
        {
            InitializeComponent();
            this.item = item;
            infoEnable.Tag = infoBox;
            limitedNameEnable.Tag = limitedNameBox;
            mobTimeEnable.Tag = mobTimeBox;
            teamEnable.Tag = teamBox;

            xInput.Value = item.X;
            yInput.Value = item.Y;
            rx0Box.Value = item.rx0Shift;
            rx1Box.Value = item.rx1Shift;
            yShiftBox.Value = item.yShift;
            LoadOptionalInt(item.Info, infoEnable, infoBox);
            LoadOptionalInt(item.Team, teamEnable, teamBox);
            LoadOptionalInt(item.MobTime, mobTimeEnable, mobTimeBox);
            LoadOptionalStr(item.LimitedName, limitedNameEnable, limitedNameBox);
            hideBox.Checked = item.Hide;

            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item, "\r\n");
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
                item.rx0Shift = (int)rx0Box.Value;
                item.rx1Shift = (int)rx1Box.Value;
                item.yShift = (int)yShiftBox.Value;
                item.MobTime = GetOptionalInt(mobTimeEnable, mobTimeBox);
                item.Info = GetOptionalInt(infoEnable, infoBox);
                item.Team = GetOptionalInt(teamEnable, teamBox);
                //item.TypeStr = GetOptionalStr(typeEnable, typeBox);
                item.LimitedName = GetOptionalStr(limitedNameEnable, limitedNameBox);
                item.Hide = hideBox.Checked;
            }
            Close();
        }

        private void enablingCheckBoxCheckChanged(object sender, EventArgs e)
        {
            CheckBox cbx = (CheckBox)sender;
            ((Control)cbx.Tag).Enabled = cbx.Checked;
        }

        private void LoadOptionalInt(int? value, CheckBox cbx, NumericUpDown box)
        {
            if (value == null) cbx.Checked = false;
            else { cbx.Checked = true; box.Value = (int)value; }
        }

        private void LoadOptionalStr(string value, CheckBox cbx, TextBox box)
        {
            if (value == null) cbx.Checked = false;
            else { cbx.Checked = true; box.Text = value; }
        }

        private int? GetOptionalInt(CheckBox cbx, NumericUpDown box)
        {
            if (cbx.Checked) return (int)box.Value;
            else return null;
        }

        private string GetOptionalStr(CheckBox cbx, TextBox box)
        {
            if (cbx.Checked) return box.Text;
            else return null;
        }
    }
}
