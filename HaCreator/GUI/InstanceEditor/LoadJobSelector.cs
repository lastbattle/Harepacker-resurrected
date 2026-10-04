using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadJobSelector : Window
    {
        private readonly List<string> jobNames = new();
        private bool accepted;
        public CharacterJob SelectedJob { get; private set; } = CharacterJob.None;

        public LoadJobSelector()
        {
            InitializeComponent();
            foreach (CharacterJob job in Enum.GetValues(typeof(CharacterJob)).Cast<CharacterJob>().OrderBy(value => (int)value))
                jobNames.Add($"[{(int)job}] - {job.GetFormattedJobName(false)}");
            SelectorDialogSupport.Filter(resultsList, jobNames, string.Empty);
            Closing += (_, _) => { if (!accepted) SelectedJob = CharacterJob.None; };
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SelectorDialogSupport.Filter(resultsList, jobNames, searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string id))
            {
                SelectedJob = (CharacterJob)int.Parse(id);
                descriptionText.Text = SelectedJob.GetFormattedJobName();
                selectButton.IsEnabled = true;
            }
            else { SelectedJob = CharacterJob.None; descriptionText.Text = string.Empty; selectButton.IsEnabled = false; }
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedJob == CharacterJob.None) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedJob = CharacterJob.None; Close(); } }
    }
}
