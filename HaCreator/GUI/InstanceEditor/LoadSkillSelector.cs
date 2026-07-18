using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadSkillSelector : Window
    {
        private readonly List<string> skillNames = new();
        private readonly int filterSkillId;
        private bool accepted;
        public int SelectedSkillId { get; private set; }

        public LoadSkillSelector(int filterSkillId)
        {
            InitializeComponent();
            this.filterSkillId = filterSkillId;
            foreach (KeyValuePair<string, Tuple<string, string>> item in Program.InfoManager.SkillNameCache)
                if (int.TryParse(item.Key, out int id) && (filterSkillId == 0 || id / 10000 == filterSkillId))
                    skillNames.Add($"[{item.Key}] - ({item.Value.Item2}) {item.Value.Item1}");
            skillNames.Sort();
            SelectorDialogSupport.Filter(resultsList, skillNames, string.Empty);
            Closing += (_, _) => { if (!accepted) SelectedSkillId = 0; };
        }
        public void searchItemInternal(string searchText) => SelectorDialogSupport.Filter(resultsList, skillNames, searchText);
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => searchItemInternal(searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            previewImage.Source = null;
            if (!SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string id) || !int.TryParse(id, out int parsed) || !Program.InfoManager.SkillNameCache.TryGetValue(id, out Tuple<string, string> info))
            { SelectedSkillId = 0; nameText.Text = descriptionText.Text = string.Empty; selectButton.IsEnabled = false; return; }
            nameText.Text = info.Item1;
            descriptionText.Text = info.Item2;
            if (Program.InfoManager.SkillWzImageCache.TryGetValue(id, out WzImageProperty image) && image?["icon"] is WzCanvasProperty icon)
                previewImage.Source = SelectorDialogSupport.ToBitmapSource(icon.GetLinkedWzCanvasBitmap());
            SelectedSkillId = parsed;
            selectButton.IsEnabled = true;
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedSkillId == 0) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedSkillId = 0; Close(); } }
    }
}
