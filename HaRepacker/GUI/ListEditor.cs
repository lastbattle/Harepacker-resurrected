using MapleLib.WzLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows;

namespace HaRepacker.GUI
{
    public partial class ListEditor : Window
    {
        private readonly WzMapleVersion version;

        public ListEditor(string path, WzMapleVersion version)
        {
            this.version = version;
            InitializeComponent();
            ApplyLocalizedText();
            UiLocalization.Apply(this);

            if (path == null)
                return;

            List<string> entries = ListFileParser.ParseListFile(path, version);
            textBox.Text = string.Join(Environment.NewLine, entries);
        }

        private void ApplyLocalizedText()
        {
            ResourceManager resources = new(typeof(ListEditor));
            CultureInfo culture = CultureInfo.CurrentUICulture;
            Title = resources.GetString("$this.Text", culture) ?? "List editor";
            saveButton.Content = resources.GetString("btnSave.Text", culture) ?? "Save";
        }

        private void ListEditor_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !Warning.Warn(UiLocalization.Translate("Are you sure you want to close this file?"));
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Title = UiLocalization.Translate("Select where to save the file"),
                Filter = UiLocalization.Translate("List WZ File (*.wz)|*.wz")
            };
            if (dialog.ShowDialog(this) != true)
                return;

            List<string> entries = textBox.Text
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            ListFileParser.SaveToDisk(dialog.FileName, version, entries);
        }
    }
}
