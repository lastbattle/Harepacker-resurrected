using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor;
using HaCreator.GUI.Localization;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.GUI
{
    public partial class New : Window
    {
        private readonly MultiBoard multiBoard;
        private readonly TabControl tabs;
        private readonly RoutedEventHandler[] rightClickHandler;

        public New(MultiBoard board, TabControl Tabs, RoutedEventHandler[] rightClickHandler)
        {
            InitializeComponent();
            multiBoard = board;
            tabs = Tabs;
            this.rightClickHandler = rightClickHandler;
            newWidth.Text = ApplicationSettings.LastMapSize.Width.ToString(CultureInfo.InvariantCulture);
            newHeight.Text = ApplicationSettings.LastMapSize.Height.ToString(CultureInfo.InvariantCulture);
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
        }

        private void CreateNew_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(newWidth.Text, out int width) || !int.TryParse(newHeight.Text, out int height) || width <= 0 || height <= 0)
            {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_PositiveDimensions"), DialogTextExtension.Get("Dialog_InvalidMapSize"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ApplicationSettings.LastMapSize = new System.Drawing.Size(width, height);
            MapLoader.CreateMap("", "<Untitled>", -1, "", true, MapLoader.CreateStandardMapMenu(rightClickHandler),
                new XNA.Point(width, height), new XNA.Point(width / 2, height / 2), tabs, multiBoard);
            DialogResult = true;
        }

        private void SelectCloneMap_Click(object sender, RoutedEventArgs e)
        {
            using Forms.NumericUpDown adapter = new Forms.NumericUpDown { Minimum = -1, Maximum = int.MaxValue, Value = -1 };
            new LoadMapSelector(adapter).ShowDialog();
            if (adapter.Value != -1)
                cloneMapId.Text = ((long)adapter.Value).ToString(CultureInfo.InvariantCulture);
        }

        private void CloneMapId_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (createCloneButton != null)
                createCloneButton.IsEnabled = long.TryParse(cloneMapId.Text, out long id) && id >= 0;
        }

        private void CreateClone_Click(object sender, RoutedEventArgs e)
        {
            if (!long.TryParse(cloneMapId.Text, out long mapId)) return;
            string mapIdText = mapId.ToString(CultureInfo.InvariantCulture);
            WzImage mapImage = WzInfoTools.FindMapImage(mapIdText, Program.WzManager);
            if (mapImage == null) { MessageBox.Show(this, DialogTextExtension.Get("Dialog_SelectedMapLoadFailed")); return; }
            string mapName = "NO NAME", streetName = "NO NAME", categoryName = "NO NAME";
            if (Program.InfoManager.MapsNameCache.TryGetValue(mapIdText, out var names))
            {
                mapName = names.Item1;
                streetName = names.Item2;
                categoryName = names.Item3;
            }
            MapInfo info = new MapInfo(mapImage, mapName, streetName, categoryName);
            MapLoader.CreateMapFromImage(-1, mapImage.DeepClone(), info, mapName, streetName, categoryName,
                tabs, multiBoard, rightClickHandler);
            DialogResult = true;
        }
    }
}
