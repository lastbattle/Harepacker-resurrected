using System.Windows;
using MapleLib.WzLib.Serializer;

namespace HaRepacker.GUI
{
    public partial class OptionsForm : ThemedDialogWindow
    {
        public OptionsForm()
        {
            InitializeComponent();
            ApplyLocalizedText();
            sortBox.IsChecked = Program.ConfigurationManager.UserSettings.Sort;
            loadRelated.IsChecked = Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles;
            apngIncompEnable.IsChecked = Program.ConfigurationManager.UserSettings.UseApngIncompatibilityFrame;
            autoAssociateBox.IsChecked = Program.ConfigurationManager.UserSettings.AutoAssociate;
            string defaultFolder = Program.ConfigurationManager.UserSettings.DefaultXmlFolder;
            defXmlFolderEnable.IsChecked = !string.IsNullOrEmpty(defaultFolder);
            defXmlFolderBox.Text = defaultFolder;
            indentBox.Text = Program.ConfigurationManager.UserSettings.Indentation.ToString();
            lineBreakBox.SelectedIndex = (int)Program.ConfigurationManager.UserSettings.LineBreakType;
            themeColorComboBox.SelectedIndex = Program.ConfigurationManager.UserSettings.ThemeColor;
            UpdateFolderState();
        }

        private string Text(string key, string fallback) => WpfDialogSupport.Text(typeof(OptionsForm), key, fallback);

        private void ApplyLocalizedText()
        {
            Title = Text("$this.Text", "Options");
            generalHeader.Text = Text("label5.Text", "General");
            sortBox.Content = Text("sortBox.Text", "Sort TreeView by default (slow!)");
            loadRelated.Content = Text("loadRelated.Text", "Auto-load related Wz files");
            apngIncompEnable.Content = Text("apngIncompEnable.Text", "Use APNG incompatibility frame");
            autoAssociateBox.Content = Text("autoAssociateBox.Text", "Automatically associate WZ files with HaRepacker");
            defXmlFolderEnable.Content = Text("defXmlFolderEnable.Text", "Default XML Folder:");
            indentationLabel.Text = Text("label1.Text", "Indentation");
            lineBreakLabel.Text = Text("label2.Text", "Line break");
            themeLabel.Text = Text("label3.Text", "Theme Color:");
            lineBreakBox.Items.Add(Text("lineBreakBox.Items", "None"));
            lineBreakBox.Items.Add(Text("lineBreakBox.Items1", "Windows"));
            lineBreakBox.Items.Add(Text("lineBreakBox.Items2", "Unix"));
            themeColorComboBox.Items.Add(Text("themeColor__comboBox.Items", "Black"));
            themeColorComboBox.Items.Add(Text("themeColor__comboBox.Items1", "White"));
            browseButton.Content = Text("browse.Text", "...");
            okButton.Content = Text("okButton.Text", "OK");
            cancelButton.Content = Text("cancelButton.Text", "Cancel");
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            int indentation = WpfDialogSupport.ParseInteger(indentBox.Text, -1);
            if (indentation < 0)
            {
                Warning.Error(Properties.Resources.OptionsIndentError);
                return;
            }
            Program.ConfigurationManager.UserSettings.Sort = sortBox.IsChecked == true;
            Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles = loadRelated.IsChecked == true;
            Program.ConfigurationManager.UserSettings.UseApngIncompatibilityFrame = apngIncompEnable.IsChecked == true;
            Program.ConfigurationManager.UserSettings.AutoAssociate = autoAssociateBox.IsChecked == true;
            Program.ConfigurationManager.UserSettings.DefaultXmlFolder = defXmlFolderEnable.IsChecked == true ? defXmlFolderBox.Text : string.Empty;
            Program.ConfigurationManager.UserSettings.Indentation = indentation;
            Program.ConfigurationManager.UserSettings.LineBreakType = (LineBreak)lineBreakBox.SelectedIndex;
            Program.ConfigurationManager.UserSettings.ThemeColor = themeColorComboBox.SelectedIndex;
            Program.ConfigurationManager.Save();
            Close();
        }

        private void browse_Click(object sender, RoutedEventArgs e) =>
            defXmlFolderBox.Text = SavedFolderBrowser.Show(Properties.Resources.SelectDefaultXmlFolder);

        private void defXmlFolderEnable_CheckedChanged(object sender, RoutedEventArgs e) => UpdateFolderState();

        private void UpdateFolderState()
        {
            bool enabled = defXmlFolderEnable.IsChecked == true;
            browseButton.IsEnabled = enabled;
            defXmlFolderBox.IsEnabled = enabled;
        }
    }
}
