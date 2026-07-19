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
    public partial class LoadNpcSelector : Window
    {
        private readonly List<string> itemNames = new();
        private bool accepted;
        public string SelectedNpcId { get; private set; } = string.Empty;

        public LoadNpcSelector()
        {
            InitializeComponent();
            itemNames.AddRange(Program.InfoManager.NpcNameCache.Select(item =>
                $"[{item.Key}] - {item.Value.Item1}{(string.IsNullOrEmpty(item.Value.Item2) ? string.Empty : $" ({item.Value.Item2})")}").OrderBy(item => item));
            SelectorDialogSupport.Filter(resultsList, itemNames, string.Empty);
            Closing += (_, _) => { if (!accepted) SelectedNpcId = string.Empty; };
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SelectorDialogSupport.Filter(resultsList, itemNames, searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            previewImage.Source = null;
            if (!SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string id) || !Program.InfoManager.NpcNameCache.TryGetValue(id, out Tuple<string, string> info))
            { SelectedNpcId = string.Empty; nameText.Text = descriptionText.Text = string.Empty; selectButton.IsEnabled = false; return; }
            nameText.Text = info.Item1;
            descriptionText.Text = info.Item2;
            if (!Program.InfoManager.NpcPropertyCache.TryGetValue(id, out WzImage image))
            {
                image = Program.FindImage("Npc", $"{id}.img");
                image?.ParseImage();
                if (image != null) lock (Program.InfoManager.NpcPropertyCache) Program.InfoManager.NpcPropertyCache.TryAdd(id, image);
            }
            if (image?["stand"]?["0"] is WzCanvasProperty canvas) previewImage.Source = SelectorDialogSupport.ToBitmapSource(canvas.GetLinkedWzCanvasBitmap());
            SelectedNpcId = id;
            selectButton.IsEnabled = true;
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedNpcId.Length == 0) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedNpcId = string.Empty; Close(); } }
    }
}
