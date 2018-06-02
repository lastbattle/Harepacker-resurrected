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
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using System.Collections;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class MapBrowser : EditorBase
    {
        public MapBrowser()
        {
            InitializeComponent();
            this.searchBox.TextChanged += this.mapBrowserCtrl.searchBox_TextChanged;
        }

        public new static int? Show()
        {
            MapBrowser mb = new MapBrowser();
            mb.ShowDialog();
            return mb.result;
        }

        private int? result = null;

        protected override void okButton_Click(object sender, EventArgs e)
        {
            loadButton_Click(sender, e);
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            result = int.Parse(mapBrowserCtrl.SelectedItem.Substring(0, 9));
            Close();
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void mapBrowserCtrl_SelectionChanged()
        {
            loadButton.Enabled = mapBrowserCtrl.LoadAvailable;
        }

        private void MapBrowser_Load(object sender, EventArgs e)
        {
            mapBrowserCtrl.InitializeMaps(false);
        }
    }
}
