using System;
using System.Windows.Forms;

namespace HaRepacker.GUI
{
    public partial class FirstRunForm : Form
    {
        public FirstRunForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Program.ConfigurationManager.UserSettings.AutoAssociate = autoAssociateBox.Checked;

            FormClosing -= FirstRunForm_FormClosing;
            Close();
        }

        private void FirstRunForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }
    }
}
