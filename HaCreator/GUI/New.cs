/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor;
using HaCreator.Wz;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.GUI
{
    public partial class New : Form
    {
        private MultiBoard multiBoard;
        private HaCreator.ThirdParty.TabPages.PageCollection Tabs;
        private EventHandler[] rightClickHandler;

        public New(MultiBoard board, HaCreator.ThirdParty.TabPages.PageCollection Tabs, EventHandler[] rightClickHandler)
        {
            InitializeComponent();
            this.multiBoard = board;
            this.Tabs = Tabs;
            this.rightClickHandler = rightClickHandler;
        }

        private void newButton_Click(object sender, EventArgs e)
        {
            MapLoader loader = new MapLoader();
            int w = int.Parse(newWidth.Text);
            int h = int.Parse(newHeight.Text);
            loader.CreateMap("<Untitled>", "", loader.CreateStandardMapMenu(rightClickHandler), new XNA.Point(w, h), new XNA.Point(w / 2, h / 2), 8, Tabs, multiBoard);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void New_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                newButton_Click(null, null);
            }
        }

        private void New_Load(object sender, EventArgs e)
        {
            newWidth.Text = ApplicationSettings.LastMapSize.Width.ToString();
            newHeight.Text = ApplicationSettings.LastMapSize.Height.ToString();
        }
    }
}
