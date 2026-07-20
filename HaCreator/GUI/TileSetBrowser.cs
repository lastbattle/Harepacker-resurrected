using HaCreator.GUI.EditorPanels;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class TileSetBrowser : Window, IDisposable
    {
        private readonly Action<string> selectTileSet;
        public TileSetBrowser(Forms.ListBox target) : this(tileSet => target.SelectedItem = tileSet) { }

        public TileSetBrowser(Action<string> selectTileSet)
        {
            this.selectTileSet = selectTileSet ?? throw new ArgumentNullException(nameof(selectTileSet));
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            Loaded += TileSetBrowser_Loaded;
        }

        private void TileSetBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (string tileSetKey in Program.InfoManager.TileSets.Keys.OrderBy(key => key))
            {
                WzImage image = Program.InfoManager.GetTileSet(tileSetKey);
                WzCanvasProperty canvas = image?["enH0"]?["0"] as WzCanvasProperty;
                if (canvas == null) continue;
                using Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                gallery.Add(bitmap, tileSetKey, tileSetKey);
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (gallery.SelectedItem?.Tag is not string tileSet) return;
            selectTileSet(tileSet);
            DialogResult = true;
        }

        public void Dispose()
        {
            if (IsVisible) Close();
        }
    }
}
