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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class EditorBase : Form
    {
        public EditorBase()
        {
            InitializeComponent();
        }

        protected virtual void InstanceEditorBase_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Escape)
            {
                cancelButton_Click(null, null);
            }
            else if (e.KeyCode == Keys.Enter)
            {
                okButton_Click(null, null);
            }
            else
            {
                e.Handled = false;
            }
        }

        protected virtual void cancelButton_Click(object sender, EventArgs e)
        {

        }

        protected virtual void okButton_Click(object sender, EventArgs e)
        {

        }
    }
}
