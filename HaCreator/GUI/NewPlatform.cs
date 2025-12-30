using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class NewPlatform : Form
    {
        public int result = 0;
        private SortedSet<int> zms;

        public NewPlatform(SortedSet<int> zms)
        {
            this.zms = zms;
            InitializeComponent();
            zmBox_ValueChanged(null, null);
        }

        private void NewPlatform_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = System.Windows.Forms.DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter && okButton.Enabled)
            {
                okButton_Click(null, null);
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            result = (int)zmBox.Value;
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void zmBox_ValueChanged(object sender, EventArgs e)
        {
            if (zms.Contains((int)zmBox.Value))
            {
                statusLabel.Text = "Already exists";
                okButton.Enabled = false;
            }
            else
            {
                statusLabel.Text = "";
                okButton.Enabled = true;
            }
        }

        private void zmBox_KeyUp(object sender, KeyEventArgs e)
        {
            zmBox_ValueChanged(null, null);
        }

        private void zmBox_KeyDown(object sender, KeyEventArgs e)
        {
            zmBox_ValueChanged(null, null);
        }

        private void zmBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            zmBox_ValueChanged(null, null);
        }
    }
}
