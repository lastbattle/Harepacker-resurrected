/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;

namespace HaRepackerLib
{
    public partial class SearchOptionsForm : Form
    {
        public SearchOptionsForm()
        {
            InitializeComponent();

            parseImages.Checked = UserSettings.ParseImagesInSearch;
            searchValues.Checked = UserSettings.SearchStringValues;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            UserSettings.ParseImagesInSearch = parseImages.Checked;
            UserSettings.SearchStringValues = searchValues.Checked;
            Close();
        }
    }
}
