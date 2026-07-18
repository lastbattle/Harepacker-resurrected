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
    public partial class LoadMobSelector : Window
    {
        private readonly List<string> itemNames = new();
        private bool accepted;
        public int SelectedMonsterId { get; private set; }

        public LoadMobSelector()
        {
            InitializeComponent();
            itemNames.AddRange(Program.InfoManager.MobNameCache.Select(item => $"[{item.Key}] - {item.Value}").OrderBy(item => item));
            SelectorDialogSupport.Filter(resultsList, itemNames, string.Empty);
            Closing += (_, _) => { if (!accepted) SelectedMonsterId = 0; };
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SelectorDialogSupport.Filter(resultsList, itemNames, searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            previewImage.Source = null;
            if (!SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string id) || !int.TryParse(id, out int mobId))
            { SelectedMonsterId = 0; descriptionText.Text = string.Empty; selectButton.IsEnabled = false; return; }
            descriptionText.Text = Program.InfoManager.MobNameCache.TryGetValue(id, out string name) ? name : "NO NAME";
            if (Program.InfoManager.MobIconCache.TryGetValue(mobId, out WzImageProperty icon) && icon is WzCanvasProperty canvas)
                previewImage.Source = SelectorDialogSupport.ToBitmapSource(canvas.GetLinkedWzCanvasBitmap());
            SelectedMonsterId = mobId;
            selectButton.IsEnabled = true;
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedMonsterId == 0) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedMonsterId = 0; Close(); } }
    }
}
