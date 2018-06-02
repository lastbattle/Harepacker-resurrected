/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
    public partial class SpawnpointInfo : Form
    {
        public SpawnPoint.Spawnpoint spawnpoint;
        public SpawnpointInfo()
        {
            InitializeComponent();
        }

        private void OKBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SpawnpointInfo_Load(object sender, EventArgs e)
        {
            XLbl.Text = ((WzIntProperty)spawnpoint.Data["x"]).Value.ToString();
            YLbl.Text = ((WzIntProperty)spawnpoint.Data["y"]).Value.ToString();
            MobIDLbl.Text = ((WzStringProperty)spawnpoint.Data["id"]).Value;
            FHIDLbl.Text = ((WzIntProperty)spawnpoint.Data["fh"]).Value.ToString();
        }
    }
}