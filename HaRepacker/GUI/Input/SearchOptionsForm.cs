using System;
using System.Windows.Forms;

namespace HaRepacker.GUI.Input
{
    public partial class SearchOptionsForm : Form
    {
        public SearchOptionsForm()
        {
            InitializeComponent();

            parseImages.Checked = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            searchValues.Checked = Program.ConfigurationManager.UserSettings.SearchStringValues;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Program.ConfigurationManager.UserSettings.ParseImagesInSearch = parseImages.Checked;
            Program.ConfigurationManager.UserSettings.SearchStringValues = searchValues.Checked;
            Close();
        }
    }
}
