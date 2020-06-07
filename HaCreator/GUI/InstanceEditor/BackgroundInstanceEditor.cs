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
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapSimulator;
using System.Windows.Controls;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class BackgroundInstanceEditor : EditorBase
    {
        public BackgroundInstance item;

        public BackgroundInstanceEditor(BackgroundInstance item)
        {
            InitializeComponent();

            this.item = item;
            xInput.Value = item.BaseX;
            yInput.Value = item.BaseY;
            if (item.Z == -1) 
                zInput.Enabled = false;
            else 
                zInput.Value = item.Z;

            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item);
            typeBox.Items.AddRange((object[])Tables.BackgroundTypeNames.Cast<object>());
            typeBox.SelectedIndex = (int)item.type;
            alphaBox.Value = item.a;
            front.Checked = item.front;
            rxBox.Value = item.rx;
            ryBox.Value = item.ry;
            cxBox.Value = item.cx;
            cyBox.Value = item.cy;

            // Resolutions
            foreach (MapRenderResolution val in Enum.GetValues(typeof(MapRenderResolution)))
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem();
                comboBoxItem.Tag = val;
                comboBoxItem.Content = val.ToString().Replace("Res_", "").Replace("_", " ").Replace("PercScaled", "% scale");

                comboBox_screenMode.Items.Add(comboBoxItem);
            }
            comboBox_screenMode.DisplayMember = "Content";

            int i = 0;
            foreach (ComboBoxItem citem in comboBox_screenMode.Items)
            {
                if ((int) ((MapRenderResolution)citem.Tag) == item.screenMode)
                {
                    comboBox_screenMode.SelectedIndex = i;
                    break;
                }
                i++;
            }
            if (item.screenMode < 0)
                comboBox_screenMode.SelectedIndex = 0;

            // Spine
            if (item.SpineAni != null)
            {
                checkBox_spineAni.Checked = true;
                textBox_spineAni.Text = item.SpineAni;
            }
            checkBox_spineRandomStart.Checked = item.SpineRandomStart;
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
                bool sort = false;
                if (xInput.Value != item.BaseX || yInput.Value != item.BaseY)
                {
                    actions.Add(UndoRedoManager.BackgroundMoved(item, new Microsoft.Xna.Framework.Point(item.BaseX, item.BaseY), new Microsoft.Xna.Framework.Point((int)xInput.Value, (int)yInput.Value)));
                    item.MoveBase((int)xInput.Value, (int)yInput.Value);
                }
                if (zInput.Enabled && item.Z != zInput.Value)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zInput.Value));
                    item.Z = (int)zInput.Value;
                    sort = true;
                }
                if (front.Checked != item.front)
                {
                    (item.front ? item.Board.BoardItems.FrontBackgrounds : item.Board.BoardItems.BackBackgrounds).Remove(item);
                    (item.front ? item.Board.BoardItems.BackBackgrounds : item.Board.BoardItems.FrontBackgrounds).Add(item);
                    item.front = front.Checked;
                    sort = true;
                }
                if (sort) item.Board.BoardItems.Sort();
                if (actions.Count > 0)
                    item.Board.UndoRedoMan.AddUndoBatch(actions);

                item.type = (BackgroundType)typeBox.SelectedIndex;
                item.a = (int)alphaBox.Value;
                item.rx = (int)rxBox.Value;
                item.ry = (int)ryBox.Value;
                item.cx = (int)cxBox.Value;
                item.cy = (int)cyBox.Value;
                item.screenMode = (int) ((MapRenderResolution)((ComboBoxItem)comboBox_screenMode.SelectedItem).Tag);  // combo box selection. 800x600, 1024x768, 1280x720, 1920x1080

                // Spine
                item.SpineRandomStart = checkBox_spineRandomStart.Checked;
                item.SpineAni = checkBox_spineAni.Checked == false ? null: textBox_spineAni.Text;
            }
            Close();
        }

        /// <summary>
        /// CheckBox changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkbox_spineAni_CheckChanged(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox checkbox = (System.Windows.Forms.CheckBox)sender;
            if (checkbox == null)
                return;

            textBox_spineAni.ReadOnly = !checkbox.Checked;
        }
    }
}
