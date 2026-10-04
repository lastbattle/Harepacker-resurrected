using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadQuestSelector : Window
    {
        private readonly List<string> questNames = new();
        private bool accepted;
        public string SelectedQuestId { get; private set; } = string.Empty;

        public LoadQuestSelector()
        {
            InitializeComponent();
            questNames.AddRange(Program.InfoManager.QuestInfos.Select(item =>
                $"[{item.Key}] - {(item.Value["name"] as WzStringProperty)?.Value ?? "NO NAME"}").OrderBy(item => item));
            SelectorDialogSupport.Filter(resultsList, questNames, string.Empty);
            Closing += (_, _) => { if (!accepted) SelectedQuestId = string.Empty; };
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SelectorDialogSupport.Filter(resultsList, questNames, searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string id))
            { SelectedQuestId = string.Empty; nameText.Text = descriptionText.Text = string.Empty; selectButton.IsEnabled = false; return; }
            SelectedQuestId = id;
            string selected = resultsList.SelectedItem as string;
            int separator = selected.IndexOf("] - ", System.StringComparison.Ordinal);
            nameText.Text = separator >= 0 ? selected[(separator + 4)..] : "NO NAME";
            descriptionText.Text = Program.InfoManager.QuestInfos.TryGetValue(id, out WzSubProperty quest)
                ? (quest["summary"] as WzStringProperty)?.Value ?? string.Empty : string.Empty;
            selectButton.IsEnabled = true;
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedQuestId.Length == 0) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedQuestId = string.Empty; Close(); } }
    }
}
