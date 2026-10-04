using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.GUI.Localization;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI
{
    public partial class LayerChange : EditorBase
    {
        List<BoardItem> items;
        Board board;

        public LayerChange(List<BoardItem> items, Board board)
        {
            this.items = items;
            this.board = board;
            InitializeComponent();
            
            foreach (Layer mapLayer in board.Layers)
            {
                layerBox.Items.Add(mapLayer.ToString());
            }
            if (board.SelectedLayerIndex == -1)
            {
                layerBox.SelectedIndex = 0;
                zmBox.SelectedIndex = 0;
            }
            else
            {
                layerBox.SelectedIndex = board.SelectedLayerIndex;
                if (board.SelectedPlatform != -1)
                {
                    zmBox.SelectedItem = board.SelectedPlatform;
                }
            }
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }


        private bool LayeredItemsSelected(out int layer)
        {
            foreach (BoardItem item in board.SelectedItems)
                if (item is LayeredItem)
                {
                    layer = ((LayeredItem)item).Layer.LayerNumber;
                    return true;
                }
            layer = 0;
            return false;
        }

        private bool LayerCapableOfHoldingSelectedItems(Layer layer)
        {
            if (layer.tS == null) return true;
            foreach (BoardItem item in items)
                if (item is TileInstance && ((TileInfo)item.BaseInfo).tS != layer.tS) return false;
            return true;
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            if (layerBox.SelectedIndex < 0 || zmBox.SelectedItem is not int zm) return;
            Layer targetLayer = board.Layers[layerBox.SelectedIndex];
            if (!LayerCapableOfHoldingSelectedItems(targetLayer))
            {
            MessageBox.Show(DialogTextExtension.Get("Dialog_LayerTileSetMismatch"), DialogTextExtension.Get("Dialog_CannotChangeLayer"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            List<UndoRedoAction> actions = new List<UndoRedoAction>();
            HashSet<int> touchedLayers = new HashSet<int>();
            foreach (BoardItem item in items)
            {
                if (!(item is IContainsLayerInfo)) continue;
                IContainsLayerInfo li = (IContainsLayerInfo)item;
                int oldLayer = li.LayerNumber;
                int oldZm = li.PlatformNumber;
                touchedLayers.Add(oldLayer);
                li.LayerNumber = targetLayer.LayerNumber;
                li.PlatformNumber = zm;
                actions.Add(UndoRedoManager.ItemLayerPlatChanged(li, new Tuple<int,int>(oldLayer, oldZm), new Tuple<int,int>(li.LayerNumber, li.PlatformNumber)));
            }
            if (actions.Count > 0)
                board.UndoRedoMan.AddUndoBatch(actions);
            touchedLayers.ToList().ForEach(x => board.Layers[x].RecheckTileSet());
            targetLayer.RecheckTileSet();
            InputHandler.ClearSelectedItems(board);
            Close();
        }

        private void LayerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (layerBox.SelectedIndex < 0) return;
            zmBox.Items.Clear();
            board.Layers[layerBox.SelectedIndex].zMList.ToList().ForEach(x => zmBox.Items.Add(x));
            zmBox.SelectedIndex = 0;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => cancelButton_Click(sender, EventArgs.Empty);
        private void Ok_Click(object sender, RoutedEventArgs e) => okButton_Click(sender, EventArgs.Empty);
    }
}
