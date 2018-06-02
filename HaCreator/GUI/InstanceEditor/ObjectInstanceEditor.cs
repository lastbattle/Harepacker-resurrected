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
using MapleLib.WzLib.WzStructure;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class ObjectInstanceEditor : EditorBase
    {
        public ObjectInstance item;

        public ObjectInstanceEditor(ObjectInstance item)
        {
            InitializeComponent();
            cxBox.Tag = cxInt;
            cyBox.Tag = cyInt;
            rxBox.Tag = rxInt;
            ryBox.Tag = ryInt;
            nameEnable.Tag = nameBox;
            questEnable.Tag = new Control[] { questAdd, questRemove, questList };
            tagsEnable.Tag = tagsBox;

            this.item = item;
            xInput.Value = item.X;
            yInput.Value = item.Y;
            zInput.Value = item.Z;
            rBox.Checked = item.r;
            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item, "\r\n");
            if (item.Name != null)
            {
                nameEnable.Checked = true;
                nameBox.Text = item.Name;
            }
            rBox.Checked = item.r;
            flowBox.Checked = item.flow;
            SetOptionalInt(rxInt, rxBox, item.rx);
            SetOptionalInt(ryInt, ryBox, item.ry);
            SetOptionalInt(cxInt, cxBox, item.cx);
            SetOptionalInt(cyInt, cyBox, item.cy);
            if (item.tags == null) tagsEnable.Checked = false;
            else { tagsEnable.Checked = true; tagsBox.Text = item.tags; }
            if (item.QuestInfo != null)
            {
                questEnable.Checked = true;
                foreach (ObjectInstanceQuest info in item.QuestInfo)
                    questList.Items.Add(info);
            }
        }

        private void SetOptionalInt(NumericUpDown intinput, CheckBox checkbox, int? num)
        {
            if (num != null) { checkbox.Checked = true; intinput.Value = (int)num; }
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
                if (zInput.Enabled && item.Z != zInput.Value)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zInput.Value));
                    item.Z = (int)zInput.Value;
                    item.Board.BoardItems.Sort();
                }
                if (actions.Count > 0)
                    item.Board.UndoRedoMan.AddUndoBatch(actions);
                item.Name = nameEnable.Checked ? nameBox.Text : null;
                item.flow = flowBox.Checked;
                item.reactor = reactorBox.Checked;
                item.r = rBox.Checked;
                item.hide = hideBox.Checked;
                item.rx = GetOptionalInt(rxInt, rxBox);
                item.ry = GetOptionalInt(ryInt, ryBox);
                item.cx = GetOptionalInt(cxInt, cxBox);
                item.cy = GetOptionalInt(cyInt, cyBox);
                item.tags = tagsEnable.Checked ? tagsBox.Text : null;
                if (questEnable.Checked)
                {
                    List<ObjectInstanceQuest> questInfo = new List<ObjectInstanceQuest>();
                    foreach (ObjectInstanceQuest info in questList.Items) questInfo.Add(info);
                    item.QuestInfo = questInfo;
                }
                else item.QuestInfo = null;
            }
            Close();
        }

        private int? GetOptionalInt(NumericUpDown intinput, CheckBox checkbox)
        {
            return checkbox.Checked ? (int?)intinput.Value : null;
        }

        private void enablingCheckBox_CheckChanged(object sender, EventArgs e)
        {
            CheckBox cbx = (CheckBox)sender;
            bool featureActivated = cbx.Checked && cbx.Enabled;
            if (cbx.Tag is Control) ((Control)cbx.Tag).Enabled = featureActivated;
            else
            {
                foreach (Control control in (Control[])cbx.Tag) control.Enabled = featureActivated;
                foreach (Control control in (Control[])cbx.Tag) if (control is CheckBox) enablingCheckBox_CheckChanged(control, e);
            }
        }

        private void questRemove_Click(object sender, EventArgs e)
        {
            if (questList.SelectedIndex != -1) questList.Items.RemoveAt(questList.SelectedIndex);
        }

        private void questAdd_Click(object sender, EventArgs e)
        {
            ObjQuestInput input = new ObjQuestInput();
            if (input.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                questList.Items.Add(input.result);
        }
    }
}
