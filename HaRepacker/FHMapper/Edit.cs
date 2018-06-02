/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
    public partial class Edit : Form
    {
        public List<Object> settings;
        public FootHold.Foothold fh;
        public Edit()
        {
            InitializeComponent();
        }

        private void CancelBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Edit_Load(object sender, EventArgs e)
        {
            PrevLbl.Text = ((WzIntProperty)fh.Data["prev"]).Value.ToString();
            NextLbl.Text = ((WzIntProperty)fh.Data["next"]).Value.ToString();
            if (!(bool)settings.ToArray()[1])
                PrevTBox.Text = ((WzIntProperty)fh.Data["prev"]).Value.ToString();
            else
                PrevTBox.Text = settings.ToArray()[0].ToString();
            if (!(bool)settings.ToArray()[3])
                NextTBox.Text = ((WzIntProperty)fh.Data["next"]).Value.ToString();
            else
                NextTBox.Text = settings.ToArray()[2].ToString();
            if (!(bool)settings.ToArray()[5])
                try {ForceTBox.Text = ((WzIntProperty)fh.Data["force"]).Value.ToString();} catch { }
            else
                ForceTBox.Text = settings.ToArray()[4].ToString();
            try { ForceLbl.Text = ((WzIntProperty)fh.Data["force"]).Value.ToString(); }
            catch { ForceLbl.Text = "None"; }
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            try
            {
                if (PrevTBox.Text != "")
                {
                    ((WzIntProperty)fh.Data["prev"]).Value = int.Parse(PrevTBox.Text);
                    fh.Data["prev"].ParentImage.Changed = true;
                }
                if (NextTBox.Text != "")
                {
                    ((WzIntProperty)fh.Data["next"]).Value = int.Parse(NextTBox.Text);
                    fh.Data["next"].ParentImage.Changed = true;
                }

                if (ForceTBox.Text != "")
                {
                    if (ForceLbl.Text == "None")
                    {
                        WzIntProperty forceProperty = new WzIntProperty("force", int.Parse(ForceTBox.Text));
                        fh.Data.AddProperty(forceProperty);
                        fh.Data.ParentImage.Changed = true;
                    }
                    else
                    {
                        ((WzIntProperty)fh.Data["force"]).Value = int.Parse(ForceTBox.Text);
                        fh.Data["force"].ParentImage.Changed = true;
                    }
                }
                this.Close();
            }
            catch { MessageBox.Show(HaRepacker.Properties.Resources.FHMapperInvalidInput, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}