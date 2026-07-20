using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadItemSelector : Window
    {
        private readonly List<string> itemNames = new();
        private readonly int filterItemCategoryId;
        private readonly InventoryType filterInventoryType;
        private bool accepted;
        public int SelectedItemId { get; private set; }

        public LoadItemSelector(int filterItemId, InventoryType filterInventoryType = InventoryType.NONE)
        {
            InitializeComponent();
            filterItemCategoryId = filterItemId;
            this.filterInventoryType = filterInventoryType;
            LoadItems();
            Closing += (_, _) => { if (!accepted) SelectedItemId = 0; };
        }
        private void LoadItems()
        {
            foreach (KeyValuePair<int, Tuple<string, string, string>> item in Program.InfoManager.ItemNameCache)
            {
                int id = item.Key;
                if (filterItemCategoryId != 0 && id / 10000 != filterItemCategoryId) continue;
                if (filterInventoryType != InventoryType.NONE && InventoryTypeExtensions.GetByType((byte)(id / 1000000)) != filterInventoryType) continue;
                itemNames.Add($"[{id}] - ({item.Value.Item1}) {item.Value.Item2}");
            }
            itemNames.Sort();
            SelectorDialogSupport.Filter(resultsList, itemNames, string.Empty);
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SelectorDialogSupport.Filter(resultsList, itemNames, searchBox.Text);
        private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            previewImage.Source = null;
            if (!SelectorDialogSupport.TryGetBracketedId(resultsList.SelectedItem as string, out string idText) || !int.TryParse(idText, out int id) || !Program.InfoManager.ItemNameCache.TryGetValue(id, out Tuple<string, string, string> info))
            { SelectedItemId = 0; descriptionText.Text = string.Empty; selectButton.IsEnabled = false; return; }
            WzCanvasProperty icon = Program.InfoManager.GetItemIcon(id, info.Item1, Program.WzManager);
            if (icon != null)
                previewImage.Source = SelectorDialogSupport.ToBitmapSource(icon.GetLinkedWzCanvasBitmap());
            descriptionText.Text = info.Item3;
            SelectedItemId = id;
            selectButton.IsEnabled = true;
        }
        private void SelectButton_Click(object sender, RoutedEventArgs e) { if (SelectedItemId == 0) return; accepted = true; Close(); }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { SelectedItemId = 0; Close(); } }
    }
}
