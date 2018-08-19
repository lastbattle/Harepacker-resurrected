/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaRepackerLib.Controls
{
    public partial class DockableSearchResult : DockContent
    {
        public DockableSearchResult()
        {
            InitializeComponent();
        }

        private void searchResultsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedIndexChanged.Invoke(sender, e);
        }

        public event EventHandler SelectedIndexChanged;
    }
}
