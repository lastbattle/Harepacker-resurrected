// Note - Foothold mapper code originally by Odecey
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
    public partial class DisplayMap : HaRepacker.GUI.ThemedDialogWindow
    {
        public List<object> settings;
        public int xOffset;
        public int yOffset;
        public double scale = 1;
        public Image map;
        public List<SpawnPoint.Spawnpoint> MobSpawnPoints;
        public List<FootHold.Foothold> Footholds;
        public List<Portals.Portal> thePortals;

        public DisplayMap()
        {
            InitializeComponent();
        }

        private void DisplayMap_FormClosing(object sender, CancelEventArgs e)
        {
            map?.Dispose();
            MapPBox.Source = null;
            MobSpawnPoints?.Clear();
            Footholds?.Clear();
            thePortals?.Clear();
        }

        private void DisplayMap_Load(object sender, EventArgs e)
        {
            if (map == null)
                return;
            using Bitmap resized = ResizeBitMap(new Bitmap(map), Math.Max(1, (int)(map.Width * scale)), Math.Max(1, (int)(map.Height * scale)));
            MapPBox.Source = BitmapToSource(resized);
            MapPBox.Width = resized.Width;
            MapPBox.Height = resized.Height;

            if (thePortals is { Count: > 0 })
            {
                Portals.Portal portal = thePortals[0];
                xOffset = (int)(((portal.Shape.X + 20) - ((WzIntProperty)portal.Data["x"]).Value) * -1);
                yOffset = (int)(((portal.Shape.Y + 20) - ((WzIntProperty)portal.Data["y"]).Value) * -1);
            }
        }

        public Bitmap ResizeBitMap(Bitmap image, int width, int height)
        {
            Bitmap result = new(width, height);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.DrawImage(image, 0, 0, width, height);
            image.Dispose();
            return result;
        }

        private static BitmapSource BitmapToSource(Bitmap bitmap)
        {
            using MemoryStream stream = new();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            BitmapImage source = new();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            source.Freeze();
            return source;
        }

        private void MapPBox_MouseClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(MapPBox);
            Rectangle hit = new((int)point.X, (int)point.Y, 1, 1);

            foreach (FootHold.Foothold foothold in Footholds ?? new List<FootHold.Foothold>())
            {
                Rectangle bounds = new((int)(foothold.Shape.X * scale), (int)(foothold.Shape.Y * scale), (int)(foothold.Shape.Width * scale), (int)(foothold.Shape.Height * scale));
                if (!bounds.IntersectsWith(hit)) continue;
                Edit editor = new() { Text = $"{HaRepacker.Properties.Resources.EditFoothold}: {foothold.Data.Name}", fh = foothold, settings = settings };
                editor.ShowDialog();
            }
            foreach (Portals.Portal portal in thePortals ?? new List<Portals.Portal>())
            {
                Rectangle bounds = new((int)(portal.Shape.X * scale), (int)(portal.Shape.Y * scale), (int)(portal.Shape.Width * scale), (int)(portal.Shape.Height * scale));
                if (!bounds.IntersectsWith(hit)) continue;
                EditPortals editor = new() { Text = $"{HaRepacker.Properties.Resources.EditPortal}: {portal.Data.Name}", portal = portal, Settings = settings };
                editor.ShowDialog();
            }
            foreach (SpawnPoint.Spawnpoint spawnpoint in MobSpawnPoints ?? new List<SpawnPoint.Spawnpoint>())
            {
                Rectangle bounds = new((int)(spawnpoint.Shape.X * scale), (int)(spawnpoint.Shape.Y * scale), (int)(spawnpoint.Shape.Width * scale), (int)(spawnpoint.Shape.Height * scale));
                if (!bounds.IntersectsWith(hit)) continue;
                SpawnpointInfo info = new() { spawnpoint = spawnpoint, Text = $"{HaRepacker.Properties.Resources.EditSP}: {spawnpoint.Data.Name}" };
                info.ShowDialog();
            }
        }

        private void MapPBox_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(MapPBox);
            Title = string.Format(HaRepacker.GUI.UiLocalization.Translate("Map X: {0} Y: {1}"),
                (int)(xOffset + point.X / scale), (int)(yOffset + point.Y / scale));
        }
    }
}
