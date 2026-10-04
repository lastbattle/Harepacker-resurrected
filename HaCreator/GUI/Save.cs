using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor;
using HaCreator.GUI.Localization;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class Save : Window
    {
        private readonly Board board;

        public Save(Board board)
        {
            this.board = board;
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            if (board.IsNewMapDesign) mapIdBox.Text = MapConstants.MaxMap.ToString();
            else switch (board.MapInfo.mapType)
            {
                case MapType.CashShopPreview:
                case MapType.ITCPreview:
                case MapType.MapLogin: mapIdBox.Text = board.MapInfo.strMapName; break;
                case MapType.RegularMap: mapIdBox.Text = board.MapInfo.id == -1 ? "-1" : board.MapInfo.id.ToString(); break;
                default: throw new NotSupportedException("Unknown map type at Save::.ctor()");
            }
            ValidateMapId();
        }

        private MapType GetMapType() => mapIdBox.Text.StartsWith("MapLogin") ? MapType.MapLogin :
            mapIdBox.Text == "CashShopPreview" ? MapType.CashShopPreview :
            mapIdBox.Text == "ITCPreview" ? MapType.ITCPreview : MapType.RegularMap;

        private void MapId_TextChanged(object sender, TextChangedEventArgs e) => ValidateMapId();

        private void ValidateMapId()
        {
            if (saveButton == null) return;
            string text = mapIdBox.Text;
            bool enabled = false;
            if (string.IsNullOrEmpty(text)) statusText.Text = DialogTextExtension.Get("Dialog_ChooseMapId");
            else if (GetMapType() != MapType.RegularMap) { statusText.Text = string.Empty; enabled = true; }
            else if (!int.TryParse(text, out int id) || id == MapConstants.MaxMap) statusText.Text = DialogTextExtension.Get("Dialog_NumericMapId");
            else if (id < MapConstants.MinMap || id > MapConstants.MaxMap) statusText.Text = DialogTextExtension.Format("Dialog_MapIdRange", MapConstants.MinMap, MapConstants.MaxMap);
            else if (Program.InfoManager.MapsNameCache.ContainsKey(id.ToString()))
            {
                statusText.Text = DialogTextExtension.Get(board.IsNewMapDesign ? "Dialog_MapIdExists" : "Dialog_MapOverwriteWarning");
                enabled = !board.IsNewMapDesign;
            }
            else { statusText.Text = string.Empty; enabled = true; }
            saveButton.IsEnabled = enabled;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using Forms.TextBox adapter = new Forms.TextBox { Text = mapIdBox.Text };
            new LoadMapSelector(adapter).ShowDialog();
            mapIdBox.Text = adapter.Text;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (board.ParentControl.UserObjects.NewObjects.Count > 0)
            {
                string objects = string.Join("\r\n", board.ParentControl.UserObjects.NewObjects.Select(item => item.l2));
                if (MessageBox.Show(this, DialogTextExtension.Format("Dialog_UnsavedObjectsPrompt", objects),
                    DialogTextExtension.Get("Dialog_UnsavedObjectsTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
                board.ParentControl.UserObjects.Flush();
            }
            MapType type = GetMapType();
            MapSaver saver = new MapSaver(board);
            board.RegenerateMinimap();
            if (type == MapType.RegularMap)
            {
                int newId = int.Parse(mapIdBox.Text);
                saver.ChangeMapTypeAndID(newId, MapType.RegularMap); saver.SaveMapImage(); saver.UpdateMapLists();
                MessageBox.Show(this, DialogTextExtension.Format("Dialog_SavedMapId", newId));
            }
            else
            {
                board.MapInfo.strMapName = mapIdBox.Text;
                ((TabItemContainer)board.TabPage.Tag).Text = board.MapInfo.strMapName;
                saver.ChangeMapTypeAndID(-1, type); saver.SaveMapImage();
                MessageBox.Show(this, DialogTextExtension.Format("Dialog_SavedMapName", board.MapInfo.strMapName));
            }
            DialogResult = true;
        }
    }
}
