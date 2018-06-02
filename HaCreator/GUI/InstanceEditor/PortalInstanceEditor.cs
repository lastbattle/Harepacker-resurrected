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
using System.Collections;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class PortalInstanceEditor : EditorBase
    {
        public PortalInstance item;
        private ControlRowManager rowMan;

        public PortalInstanceEditor(PortalInstance item)
        {
            InitializeComponent();
            int portalTypes = Program.InfoManager.PortalTypeById.Count;
            object[] portals = new object[portalTypes];
            for (int i = 0; i < portalTypes; i++)
            {
                portals[i] = Tables.PortalTypeNames[Program.InfoManager.PortalTypeById[i]];
            }
            ptComboBox.Items.AddRange(portals);
            this.item = item;

            rowMan = new ControlRowManager(new ControlRow[] { 
            new ControlRow(new Control[] { pnLabel, pnBox }, 26, "pn"),
            new ControlRow(new Control[] { tmLabel, tmBox, btnBrowseMap, thisMap }, 26, "tm"),
            new ControlRow(new Control[] { tnLabel, tnBox, btnBrowseTn, leftBlankLabel }, 26, "tn"),
            new ControlRow(new Control[] { scriptLabel, scriptBox }, 26, "script"),
            new ControlRow(new Control[] { delayEnable, delayBox }, 26, "delay"),
            new ControlRow(new Control[] { rangeEnable, xRangeLabel, hRangeBox, yRangeLabel, vRangeBox }, 26, "range"),
            new ControlRow(new Control[] { impactLabel, hImpactEnable, hImpactBox, vImpactEnable, vImpactBox }, 26, "impact"),
            new ControlRow(new Control[] { hideTooltip, onlyOnce }, 26, "bool"),
            new ControlRow(new Control[] { imageLabel, portalImageList, portalImageBox }, okButton.Top - portalImageList.Top, "image"),
            new ControlRow(new Control[] { okButton, cancelButton }, 26, "buttons")
        }, this);

            delayEnable.Tag = delayBox;
            hImpactEnable.Tag = hImpactBox;
            vImpactEnable.Tag = vImpactBox;

            xInput.Value = item.X;
            yInput.Value = item.Y;
            ptComboBox.SelectedIndex = Program.InfoManager.PortalIdByType[item.pt];
            pnBox.Text = item.pn;
            if (item.tm == item.Board.MapInfo.id) thisMap.Checked = true;
            else tmBox.Value = item.tm;
            tnBox.Text = item.tn;
            if (item.script != null) scriptBox.Text = item.script;
            SetOptionalInt(item.delay, delayEnable, delayBox);
            SetOptionalInt(item.hRange, rangeEnable, hRangeBox);
            SetOptionalInt(item.vRange, rangeEnable, vRangeBox);
            SetOptionalInt(item.horizontalImpact, hImpactEnable, hImpactBox);
            if (item.verticalImpact != null) vImpactBox.Value = (int)item.verticalImpact;
            onlyOnce.Checked = item.onlyOnce;
            hideTooltip.Checked = item.hideTooltip;
            if (item.image != null)
            {
                portalImageList.SelectedItem = item.image;
            }
        }

        private void SetOptionalInt(int? value, CheckBox enabler, NumericUpDown input)
        {
            if (value == null) enabler.Checked = false;
            else { enabler.Checked = true; input.Value = (int)value; }
        }

        private int? GetOptionalInt(CheckBox enabler, NumericUpDown input)
        {
            return enabler.Checked ? (int?)input.Value : null;
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

                item.pt = Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex];
                switch (item.pt)
                {
                    case PortalType.PORTALTYPE_STARTPOINT:
                        item.pn = "sp";
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = null;
                        item.script = null;
                        item.onlyOnce = null;
                        item.hideTooltip = null;
                        break;
                    case PortalType.PORTALTYPE_INVISIBLE:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_VISIBLE:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_COLLISION:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = GetOptionalInt(rangeEnable, hRangeBox);
                        item.vRange = GetOptionalInt(rangeEnable, vRangeBox);
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_CHANGABLE:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_CHANGABLE_INVISIBLE:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_TOWNPORTAL_POINT:
                        item.pn = "tp";
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = null;
                        item.script = null;
                        item.onlyOnce = null;
                        item.hideTooltip = null;
                        break;
                    case PortalType.PORTALTYPE_SCRIPT:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = null;
                        item.script = scriptBox.Text;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_SCRIPT_INVISIBLE:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = null;
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_COLLISION_SCRIPT:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = scriptBox.Text;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_HIDDEN:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_SCRIPT_HIDDEN:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = null;
                        item.verticalImpact = null;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_COLLISION_CUSTOM_IMPACT:
                        item.pn = pnBox.Text;
                        item.tm = 999999999;
                        item.tn = "";
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = GetOptionalInt(hImpactEnable, hImpactBox);
                        item.verticalImpact = (int)vImpactBox.Value;
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                    case PortalType.PORTALTYPE_COLLISION_UNKNOWN_PCIG:
                        item.pn = pnBox.Text;
                        item.tm = thisMap.Checked ? item.Board.MapInfo.id : (int)tmBox.Value;
                        item.tn = tnBox.Text;
                        item.hRange = null;
                        item.vRange = null;
                        item.horizontalImpact = GetOptionalInt(hImpactEnable, hImpactBox);
                        item.verticalImpact = GetOptionalInt(vImpactEnable, vImpactBox);
                        item.delay = GetOptionalInt(delayEnable, delayBox);
                        item.script = null;
                        item.onlyOnce = onlyOnce.Checked;
                        item.hideTooltip = hideTooltip.Checked;
                        break;
                }

                if (portalImageList.SelectedItem != null && Program.InfoManager.GamePortals.ContainsKey(Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex]))
                {
                    item.image = (string)portalImageList.SelectedItem;
                }
            }
            Close();
        }

        private void thisMap_CheckedChanged(object sender, EventArgs e)
        {
            tmBox.Enabled = !thisMap.Checked;
            btnBrowseMap.Enabled = !thisMap.Checked;
            btnBrowseTn.Enabled = thisMap.Checked;
        }

        private void EnablingCheckBoxCheckChanged(object sender, EventArgs e)
        {
            ((Control)((CheckBox)sender).Tag).Enabled = ((CheckBox)sender).Checked;
        }

        private void ptComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnBrowseTn.Enabled = thisMap.Checked;
            switch (Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex])
            {
                case PortalType.PORTALTYPE_STARTPOINT:
                    rowMan.SetInvisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetInvisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetInvisible("bool");
                    break;
                case PortalType.PORTALTYPE_INVISIBLE:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_VISIBLE:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_COLLISION:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetVisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_CHANGABLE:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_CHANGABLE_INVISIBLE:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_TOWNPORTAL_POINT:
                    rowMan.SetInvisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetInvisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetInvisible("bool");
                    break;
                case PortalType.PORTALTYPE_SCRIPT:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetVisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_SCRIPT_INVISIBLE:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetVisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_COLLISION_SCRIPT:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetVisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetVisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_HIDDEN:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_SCRIPT_HIDDEN:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetInvisible("tn");
                    rowMan.SetVisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetInvisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_COLLISION_CUSTOM_IMPACT:
                    rowMan.SetVisible("pn");
                    rowMan.SetInvisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetVisible("impact");
                    rowMan.SetVisible("bool");
                    break;
                case PortalType.PORTALTYPE_COLLISION_UNKNOWN_PCIG:
                    rowMan.SetVisible("pn");
                    rowMan.SetVisible("tm");
                    rowMan.SetVisible("tn");
                    rowMan.SetInvisible("script");
                    rowMan.SetVisible("delay");
                    rowMan.SetInvisible("range");
                    rowMan.SetVisible("impact");
                    rowMan.SetVisible("bool");
                    break;
            }
            string pt = Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex];
            leftBlankLabel.Visible = pt == PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP;
            if (pt == PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP)
                btnBrowseTn.Enabled = true;
            if (!Program.InfoManager.GamePortals.ContainsKey(pt)) 
                rowMan.SetInvisible("image");
            else
            {
                portalImageList.Items.Clear();
                portalImageList.Items.Add("default");
                portalImageBox.Image = null;
                rowMan.SetVisible("image");
                foreach (DictionaryEntry image in Program.InfoManager.GamePortals[pt])
                    portalImageList.Items.Add(image.Key);
                portalImageList.SelectedIndex = 0;
            }
        }

        private void portalImageList_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (item.Board.ParentControl)
            {
                if (portalImageList.SelectedItem == null) 
                    return;
                else if ((string)portalImageList.SelectedItem == "default") 
                    portalImageBox.Image = new Bitmap(Program.InfoManager.GamePortals[Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex]].DefaultImage);
                else 
                    portalImageBox.Image = new Bitmap(Program.InfoManager.GamePortals[Program.InfoManager.PortalTypeById[ptComboBox.SelectedIndex]][(string)portalImageList.SelectedItem]);
            }
        }

        private void rangeEnable_CheckedChanged(object sender, EventArgs e)
        {
            hRangeBox.Enabled = rangeEnable.Checked;
            vRangeBox.Enabled = rangeEnable.Checked;
        }

        private void btnBrowseMap_Click(object sender, EventArgs e)
        {
            int? mapId = MapBrowser.Show();
            if (mapId != null) tmBox.Value = (int)mapId;
        }

        private void btnBrowseTn_Click(object sender, EventArgs e)
        {
            string tn = TnSelector.Show(item.Board);
            if (tn != null) tnBox.Text = tn;
        }
    }

    public class ControlRow
    {
        public Control[] controls;
        public bool invisible = false;
        public int rowSize;
        public string rowName;

        public ControlRow(Control[] controls, int rowSize, string rowName)
        {
            this.controls = controls;
            this.rowSize = rowSize;
            this.rowName = rowName;
        }
    }

    public class ControlRowManager
    {
        ControlRow[] rows;
        Hashtable names = new Hashtable();
        Form form;

        public ControlRowManager(ControlRow[] rows, Form form)
        {
            this.form = form;
            this.rows = rows;
            int index = 0;
            foreach (ControlRow row in rows)
                names[row.rowName] = index++;
        }

        public void SetInvisible(string name)
        {
            SetInvisible((int)names[name]);
        }

        public void SetInvisible(int index)
        {
            ControlRow row = rows[index];
            if (row.invisible) return;
            row.invisible = true;
            foreach (Control c in row.controls)
                c.Visible = false;
            int size = row.rowSize;
            for (int i = index + 1; i < rows.Length; i++)
            {
                row = rows[i];
                foreach (Control c in row.controls)
                    c.Location = new Point(c.Location.X, c.Location.Y - size);
            }
            form.Height -= size;
        }

        public void SetVisible(string name)
        {
            SetVisible((int)names[name]);
        }

        public void SetVisible(int index)
        {
            ControlRow row = rows[index];
            if (!row.invisible) return;
            row.invisible = false;
            foreach (Control c in row.controls)
                c.Visible = true;
            int size = row.rowSize;
            for (int i = index + 1; i < rows.Length; i++)
            {
                row = rows[i];
                foreach (Control c in row.controls)
                    c.Location = new Point(c.Location.X, c.Location.Y + size);
            }
            form.Height += size;
        }
    }
}
