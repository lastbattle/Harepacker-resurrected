/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
    public partial class EditPortals : Form
    {
        public List<Object> Settings;
        public Portals.Portal portal;
        public EditPortals()
        {
            InitializeComponent();
        }

        private void EditPortals_Load(object sender, EventArgs e)
        {
            TypeLbl.Text = ((WzIntProperty)portal.Data["pt"]).Value.ToString();
            DestLbl.Text = ((WzIntProperty)portal.Data["tm"]).Value.ToString();
            XPosLbl.Text = ((WzIntProperty)portal.Data["x"]).Value.ToString();
            YPosLbl.Text = ((WzIntProperty)portal.Data["y"]).Value.ToString();
            if (!(bool)Settings.ToArray()[11])
                TypeTBox.Text = ((WzIntProperty)portal.Data["pt"]).Value.ToString();
            else
                TypeTBox.Text = Settings.ToArray()[10].ToString();
            if (!(bool)Settings.ToArray()[7])
                XTBox.Text = ((WzIntProperty)portal.Data["x"]).Value.ToString();
            else
                XTBox.Text = Settings.ToArray()[6].ToString();
            if (!(bool)Settings.ToArray()[9])
                YTBox.Text = ((WzIntProperty)portal.Data["y"]).Value.ToString();
            else
                YTBox.Text = Settings.ToArray()[8].ToString();
        }

        private void ConfirmBtn_Click(object sender, EventArgs e)
        {
            try
            {
                if (TypeTBox.Text != "")
                {
                    ((WzIntProperty)portal.Data["pt"]).Value = int.Parse(TypeTBox.Text);
                    portal.Data["pt"].ParentImage.Changed = true;
                }
                if (XTBox.Text != "")
                {
                    ((WzIntProperty)portal.Data["x"]).Value = int.Parse(XTBox.Text);
                    portal.Data["x"].ParentImage.Changed = true;
                }
                if (YTBox.Text != "")
                {
                    ((WzIntProperty)portal.Data["y"]).Value = int.Parse(YTBox.Text);
                    portal.Data["y"].ParentImage.Changed = true;
                }
            }
            catch (FormatException) { MessageBox.Show(HaRepacker.Properties.Resources.FHMapperInvalidInput, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            this.Close();

        }
    }
}