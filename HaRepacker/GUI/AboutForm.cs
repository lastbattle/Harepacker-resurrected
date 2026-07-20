using System;
using System.Diagnostics;
using System.Windows;

namespace HaRepacker.GUI
{
    public partial class AboutForm : ThemedDialogWindow
    {
        public AboutForm()
        {
            InitializeComponent();
            ApplyLocalizedText();
        }

        private string Text(string key, string fallback) => WpfDialogSupport.Text(typeof(AboutForm), key, fallback);

        private void ApplyLocalizedText()
        {
            Title = Text("$this.Text", "About");
            productName.Text = Text("label1.Text", "HaRepacker");
            developerText.Text = Text("label2.Text", "Developed by haha01haha01");
            creditsText.Text = Text("label3.Text", "Thanks to Snow for MapleLib");
            copyrightText.Text = Text("label4.Text", "HaRepacker - WZ extractor and repacker\nCopyright (C) 2009-2015 haha01haha01");
            forkText.Text = Text("label5.Text", "A fork~");
            repositoryLink.Content = Text("linkLabel1.Text", "https://github.com/lastbattle/Harepacker-resurrected");
            okButton.Content = Text("button1.Text", "OK");
        }

        private void button1_Click(object sender, RoutedEventArgs e) => Close();

        private void linkLabel1_LinkClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(repositoryLink.Content?.ToString() ?? string.Empty)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(UiLocalization.Translate("An error occurred: {0}"), ex.Message),
                    UiLocalization.Translate("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
