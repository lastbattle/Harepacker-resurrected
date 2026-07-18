using System;
using System.Linq;
using System.Windows;
using HaRepacker.GUI.Panels;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib;

namespace HaRepacker.GUI
{
    public partial class NewForm : ThemedDialogWindow
    {
        private readonly MainPanel _mainPanel;
        private bool _isLoaded;
        private readonly int _defaultVersionIndex;

        public NewForm(MainPanel panel)
        {
            _mainPanel = panel;
            InitializeComponent();
            ApplyLocalizedText();
            WzEncryptionUiShared.Populate(encryptionBox);
            SetWzEncryptionBoxSelectionByWzMapleVersion();
            _defaultVersionIndex = encryptionBox.SelectedIndex;
            versionBox.Text = "1";
            Closed += (_, _) => encryptionBox.SelectedIndex = _defaultVersionIndex;
            _isLoaded = true;
            UpdateTypeState();
        }

        private string Text(string key, string fallback) => WpfDialogSupport.Text(typeof(NewForm), key, fallback);

        private void ApplyLocalizedText()
        {
            Title = Text("$this.Text", "New...");
            nameLabel.Text = Text("label1.Text", "Name");
            extensionLabel.Text = Text("label2.Text", ".wz");
            typeHeader.Text = Text("label3.Text", "Type");
            regBox.Content = Text("regBox.Text", "Regular");
            listBox.Content = Text("listBox.Text", "List");
            hotfixBox.Content = Text("radioButton_hotfix.Text", "Hotfix Data.wz");
            copyrightLabel.Text = Text("label4.Text", "Copyright");
            encryptionLabel.Text = Text("label5.Text", "Encryption");
            versionLabel.Text = Text("label6.Text", "Version");
            okButton.Content = Text("okButton.Text", "OK");
            cancelButton.Content = Text("cancelButton.Text", "Cancel");
        }

        private void EncryptionBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isLoaded || encryptionBox.SelectedItem is not EncryptionKey selectedEncryption)
                return;
            if (selectedEncryption.MapleVersion == WzMapleVersion.CUSTOM)
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            else
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();
        }

        private void SetWzEncryptionBoxSelectionByWzMapleVersion()
        {
            WzMapleVersion version = Program.ConfigurationManager.ApplicationSettings.MapleVersion;
            encryptionBox.SelectedIndex = MainForm.GetIndexByWzMapleVersion(version, true);
            if (version == WzMapleVersion.CUSTOM)
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
        }

        private void Type_CheckedChanged(object sender, RoutedEventArgs e) => UpdateTypeState();

        private void UpdateTypeState()
        {
            if (hotfixBox == null || copyrightBox == null || versionBox == null)
                return;

            bool supportsHeader = hotfixBox.IsChecked != true;
            copyrightBox.IsEnabled = supportsHeader;
            versionBox.IsEnabled = supportsHeader;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            string name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || encryptionBox.SelectedItem is not EncryptionKey selectedEncryption)
                return;

            if (regBox.IsChecked == true)
            {
                WzFile file = new((short)WpfDialogSupport.ParseInteger(versionBox.Text, 1), selectedEncryption.MapleVersion);
                file.Header.Copyright = copyrightBox.Text;
                file.Header.RecalculateFileStart();
                file.Name = name + ".wz";
                file.WzDirectory.Name = name + ".wz";
                _mainPanel.DataTree.Nodes.Add(new WzNode(file));
            }
            else if (listBox.IsChecked == true)
            {
                new ListEditor(null, selectedEncryption.MapleVersion).Show();
            }
            else if (hotfixBox.IsChecked == true)
            {
                WzImage image = new(name + ".wz");
                image.MarkWzImageAsParsed();
                _mainPanel.DataTree.Nodes.Add(new WzNode(image));
            }
            Close();
        }
    }
}
