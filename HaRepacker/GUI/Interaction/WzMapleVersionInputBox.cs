using System;
using System.Windows;
using HaRepacker.GUI;
using MapleLib.Configuration;
using MapleLib.WzLib;

namespace HaRepacker.GUI.Interaction
{
    public partial class WzMapleVersionInputBox : ThemedDialogWindow
    {
        public static bool Show(string title, out WzMapleVersion mapleVersionEncryptionSelected)
        {
            using WzMapleVersionInputBox form = new(title);
            bool result = form.ShowDialog() == System.Windows.Forms.DialogResult.OK;
            mapleVersionEncryptionSelected = result
                ? (form.comboBox_wzEncryptionType.SelectedItem as EncryptionKey)?.MapleVersion ?? WzMapleVersion.BMS
                : WzMapleVersion.BMS;
            return result;
        }

        public WzMapleVersionInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            WzEncryptionUiShared.Populate(comboBox_wzEncryptionType);
            comboBox_wzEncryptionType.SelectedIndex = MainForm.GetIndexByWzMapleVersion(Program.ConfigurationManager.ApplicationSettings.MapleVersion);
            label_wzEncrytionType.Text = Properties.Resources.InteractionWzMapleVersionInfo;
            okButton.Content = WpfDialogSupport.Text(typeof(WzMapleVersionInputBox), "okButton.Text", "OK");
            cancelButton.Content = WpfDialogSupport.Text(typeof(WzMapleVersionInputBox), "cancelButton.Text", "Cancel");
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (comboBox_wzEncryptionType.SelectedIndex < 0)
            {
                MessageBox.Show(Properties.Resources.EnterValidInput, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void comboBox_Encryption_SelectedIndexChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }
    }
}
