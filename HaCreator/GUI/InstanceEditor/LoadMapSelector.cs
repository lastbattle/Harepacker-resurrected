/* Copyright (C) 2020 lastbattle

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

//uncomment the line below to create a space-time tradeoff (saving RAM by wasting more CPU cycles)
#define SPACETIME

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Xml;
using System.Linq;
using System.IO;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapEditor;
using XNA = Microsoft.Xna.Framework;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using MapleLib.Helpers;
using HaCreator.Wz;
using MapleLib.WzLib.Serialization;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadMapSelector : System.Windows.Forms.Form
    {
        /// <summary>
        /// The NumericUpDown text to set upon selection
        /// </summary>
        private NumericUpDown numericUpDown;

        public LoadMapSelector(NumericUpDown numericUpDown)
        {
            InitializeComponent();

            DialogResult = DialogResult.Cancel;

            this.numericUpDown = numericUpDown;
            this.searchBox.TextChanged += this.mapBrowser.searchBox_TextChanged;
        }

        private void Load_Load(object sender, EventArgs e)
        {
            this.mapBrowser.InitializeMaps(false); // load list of maps without Cash Shop, Login, etc
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            string mapid = mapBrowser.SelectedItem.Substring(0, 9);
            string mapcat = "Map" + mapid.Substring(0, 1);


            this.numericUpDown.Value = long.Parse(mapid);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void mapBrowser_SelectionChanged()
        {
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                loadButton_Click(null, null);
            }
        }
    }
}
