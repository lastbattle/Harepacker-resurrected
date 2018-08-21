/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace HaRepacker.GUI
{
    public partial class SearchSelectionForm : Form
    {
        // Events
        public delegate void SearchSelectionChanged(string str);
        public event SearchSelectionChanged OnSelectionChanged;

        public SearchSelectionForm()
        {
            InitializeComponent();
        }

        public static SearchSelectionForm Show(List<string> searchPaths)
        {
            SearchSelectionForm form = new SearchSelectionForm();
            foreach (string item in searchPaths)
            {
                form.listBox_items.Items.Add(item);
            }
            form.Show();
            form.BringToFront();

            return form;
        }

        /// <summary>
        /// On Item selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_items_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedItem = listBox_items.SelectedItem as string;
            if (selectedItem != null)
            {
                OnSelectionChanged?.Invoke(selectedItem);
            }
        }
    }
}
