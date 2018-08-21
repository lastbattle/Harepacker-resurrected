/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaRepacker.Configuration;
using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public partial class SearchOptionsForm : Form
    {
        public SearchOptionsForm()
        {
            InitializeComponent();

            parseImages.Checked = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            searchValues.Checked = Program.ConfigurationManager.UserSettings.SearchStringValues;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Program.ConfigurationManager.UserSettings.ParseImagesInSearch = parseImages.Checked;
            Program.ConfigurationManager.UserSettings.SearchStringValues = searchValues.Checked;
            Close();
        }
    }
}
