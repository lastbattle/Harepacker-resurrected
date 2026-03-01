using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public partial class FloatingPointInputBox : Form
    {
        public static bool Show(string title, out string name, out double? value)
        {
            FloatingPointInputBox form = new FloatingPointInputBox(title);
            bool result = form.ShowDialog() == DialogResult.OK;
            name = form.nameResult;
            value = form.doubleResult;
            return result;
        }

        private string nameResult = null;
        private double? doubleResult = null;

        public FloatingPointInputBox(string title)
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
            Text = title;
        }

        private void nameBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                okButton_Click(null, null);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            if (nameBox.Text != "" && nameBox.Text != null)
            {
                nameResult = nameBox.Text;
                doubleResult = valueBox.Value;
                DialogResult = DialogResult.OK;
                Close();
            }
            else MessageBox.Show(Properties.Resources.EnterValidInput, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
