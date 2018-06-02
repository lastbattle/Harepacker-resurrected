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
using HaCreator.MapEditor.Instance.Shapes;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class FootholdEditor : EditorBase
    {
        private FootholdLine[] footholds;

        public FootholdEditor(FootholdLine[] footholds)
        {
            this.footholds = footholds;
            InitializeComponent();

            int? force = footholds[0].Force;
            int? piece = footholds[0].Piece;
            bool cantThrough = footholds[0].CantThrough;
            bool forbidFallDown = footholds[0].ForbidFallDown;
            bool indeterminate = false;
            for (int i = 1; i < footholds.Length; i++)
                if (footholds[i].Force != force) { indeterminate = true; break; }
            if (indeterminate) forceEnable.CheckState = CheckState.Indeterminate;
            else if (forceEnable.Checked = force != null) forceInt.Value = (int)force;

            indeterminate = false;
            for (int i = 1; i < footholds.Length; i++)
                if (footholds[i].Piece != piece) { indeterminate = true; break; }
            if (indeterminate) pieceEnable.CheckState = CheckState.Indeterminate;
            else if (pieceEnable.Checked = force != null) pieceInt.Value = (int)piece;

            indeterminate = false;
            for (int i = 1; i < footholds.Length; i++)
                if (footholds[i].CantThrough != cantThrough) { indeterminate = true; break; }
            if (indeterminate) cantThroughBox.CheckState = CheckState.Indeterminate;
            else cantThroughBox.Checked = cantThrough;

            indeterminate = false;
            for (int i = 1; i < footholds.Length; i++)
                if (footholds[i].ForbidFallDown != forbidFallDown) { indeterminate = true; break; }
            if (indeterminate) forbidFallDownBox.CheckState = CheckState.Indeterminate;
            else forbidFallDownBox.Checked = forbidFallDown;
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void forceEnable_CheckedChanged(object sender, EventArgs e)
        {
            forceInt.Enabled = forceEnable.CheckState == CheckState.Checked;
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            if (footholds.Length == 0)
            {
                return;
            }
            lock (footholds[0].Board.ParentControl)
            {
                if (forceEnable.CheckState != CheckState.Indeterminate)
                {
                    int? force = forceEnable.Checked ? (int?)forceInt.Value : (int?)null;
                    foreach (FootholdLine line in footholds) line.Force = force;
                }
                if (pieceEnable.CheckState != CheckState.Indeterminate)
                {
                    int? piece = pieceEnable.Checked ? (int?)pieceInt.Value : (int?)null;
                    foreach (FootholdLine line in footholds) line.Piece = piece;
                }
                if (cantThroughBox.CheckState != CheckState.Indeterminate)
                    foreach (FootholdLine line in footholds) line.CantThrough = cantThroughBox.Checked;
                if (forbidFallDownBox.CheckState != CheckState.Indeterminate)
                    foreach (FootholdLine line in footholds) line.ForbidFallDown = forbidFallDownBox.Checked;
            }
            Close();
        }
    }
}
