/* Copyright (C) 2022 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
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
using HaCreator.MapEditor.Instance.Misc;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class MirrorFieldEditor : EditorBase
    {
        public MirrorFieldData _mirrorFieldData;

        public MirrorFieldEditor(MirrorFieldData mirrorFieldData)
        {
            InitializeComponent();
            this._mirrorFieldData = mirrorFieldData;


            checkBox_reflection.Checked = mirrorFieldData.ReflectionInfo.Reflection;
            checkBox_alphaTest.Checked = mirrorFieldData.ReflectionInfo.AlphaTest;

            trackBar_gradient.Value = mirrorFieldData.ReflectionInfo.Gradient;
            trackBar_alpha.Value = mirrorFieldData.ReflectionInfo.Alpha;

            numericUpDown_xOffsetValue.Value = (decimal)mirrorFieldData.Offset.X;
            numericUpDown_yOffsetValue.Value = (decimal) mirrorFieldData.Offset.Y;

            comboBox_objectForOverlay.SelectedIndex = 0; // "mirror"
            int i = 0;
            foreach (string comboBoxItem in comboBox_objectForOverlay.Items)
            {
                if (comboBoxItem == mirrorFieldData.ReflectionInfo.ObjectForOverlay)
                {
                    comboBox_objectForOverlay.SelectedIndex = i;
                    break;
                }
            }

            //numericUpDown_xOffsetValue.Value = mirrorFieldData.

            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(_mirrorFieldData);

            /*rBox.Checked = this._mirrorFieldData.r;
            flipBox.Checked = this._mirrorFieldData.Flip;
            hideBox.Checked = !this._mirrorFieldData.hide.HasValue ? false : this._mirrorFieldData.hide.Value;
            if (this._mirrorFieldData.Name != null)
            {
                nameEnable.Checked = true;
                nameBox.Text = this._mirrorFieldData.Name;
            }
            flowBox.Checked = this._mirrorFieldData.flow;

            SetOptionalInt(rxInt, rxBox, this._mirrorFieldData.rx);
            SetOptionalInt(ryInt, ryBox, this._mirrorFieldData.ry);
            SetOptionalInt(cxInt, cxBox, this._mirrorFieldData.cx);
            SetOptionalInt(cyInt, cyBox, this._mirrorFieldData.cy);

            if (this._mirrorFieldData.tags == null) 
                tagsEnable.Checked = false;
            else 
            { 
                tagsEnable.Checked = true; tagsBox.Text = this._mirrorFieldData.tags; 
            }*/
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            lock (this._mirrorFieldData.Board.ParentControl)
            {
                this._mirrorFieldData.ReflectionInfo.Reflection = checkBox_reflection.Checked == true;
                this._mirrorFieldData.ReflectionInfo.AlphaTest = checkBox_alphaTest.Checked == true;

                this._mirrorFieldData.ReflectionInfo.Gradient = (ushort) trackBar_gradient.Value;
                this._mirrorFieldData.ReflectionInfo.Alpha = (ushort) trackBar_alpha.Value;

                this._mirrorFieldData.Offset = new Microsoft.Xna.Framework.Vector2((int)numericUpDown_xOffsetValue.Value, (int) numericUpDown_yOffsetValue.Value);

                // combobox for objectForOverlay
                this._mirrorFieldData.ReflectionInfo.ObjectForOverlay = comboBox_objectForOverlay.SelectedItem as string;

                /*List<UndoRedoAction> actions = new List<UndoRedoAction>();
                if (xInput.Value != this._mirrorFieldData.X || yInput.Value != this._mirrorFieldData.Y)
                {
                    actions.Add(UndoRedoManager.ItemMoved(
                        this._mirrorFieldData, 
                        new Microsoft.Xna.Framework.Point(this._mirrorFieldData.X, this._mirrorFieldData.Y), 
                        new Microsoft.Xna.Framework.Point((int)xInput.Value, (int)yInput.Value)));
                    this._mirrorFieldData.Move((int)xInput.Value, (int)yInput.Value);
                }
                if (zInput.Enabled && this._mirrorFieldData.Z != zInput.Value)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(this._mirrorFieldData, this._mirrorFieldData.Z, (int)zInput.Value));
                    this._mirrorFieldData.Z = (int)zInput.Value;
                    this._mirrorFieldData.Board.BoardItems.Sort();
                }
                if (actions.Count > 0)
                    this._mirrorFieldData.Board.UndoRedoMan.AddUndoBatch(actions);*/

                /* this._mirrorFieldData.Name = nameEnable.Checked ? nameBox.Text : null;
                 this._mirrorFieldData.flow = flowBox.Checked;
                 this._mirrorFieldData.reactor = reactorBox.Checked;
                 this._mirrorFieldData.r = rBox.Checked;
                 this._mirrorFieldData.Flip = flipBox.Checked;
                 this._mirrorFieldData.hide = hideBox.Checked;
                 this._mirrorFieldData.rx = GetOptionalInt(rxInt, rxBox);
                 this._mirrorFieldData.ry = GetOptionalInt(ryInt, ryBox);
                 this._mirrorFieldData.cx = GetOptionalInt(cxInt, cxBox);
                 this._mirrorFieldData.cy = GetOptionalInt(cyInt, cyBox);
                 this._mirrorFieldData.tags = tagsEnable.Checked ? tagsBox.Text : null;*/
            }
            Close();
        }
        private void enablingCheckBox_CheckChanged(object sender, EventArgs e)
        {
            CheckBox cbx = (CheckBox)sender;
            bool featureActivated = cbx.Checked && cbx.Enabled;

            if (cbx.Tag is Control) 
                ((Control)cbx.Tag).Enabled = featureActivated;
            else
            {
                foreach (Control control in (Control[])cbx.Tag) 
                    control.Enabled = featureActivated;

                foreach (Control control in (Control[])cbx.Tag) 
                    if (control is CheckBox) 
                        enablingCheckBox_CheckChanged(control, e);
            }
        }

        /// <summary>
        /// On trackbar gradient value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void trackBar_gradient_ValueChanged(object sender, EventArgs e)
        {
            if (_mirrorFieldData == null || label_gradient == null)
                return;

            label_gradient.Text = _mirrorFieldData.ReflectionInfo.Gradient.ToString();
        }

        /// <summary>
        /// On trackbar alpha value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void trackBar_alpha_ValueChanged(object sender, EventArgs e)
        {
            if (_mirrorFieldData == null || label_alphaValue == null)
                return;

            label_alphaValue.Text = _mirrorFieldData.ReflectionInfo.Alpha.ToString();
        }
    }
}