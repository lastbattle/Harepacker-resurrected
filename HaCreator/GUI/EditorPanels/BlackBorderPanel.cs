using HaCreator.MapEditor;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class BlackBorderPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private bool isUpdating;

        public BlackBorderPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            hcsm.SetBlackBorderPanel(this);
        }

        public void UpdateBoardData()
        {
            var selectedBoard = hcsm?.MultiBoard.SelectedBoard;
            if (selectedBoard == null)
                return;

            isUpdating = true;
            SetValue(checkBox_top, numericUpDown_top, selectedBoard.MapInfo.LBTop);
            SetValue(checkBox_bottom, numericUpDown_bottom, selectedBoard.MapInfo.LBBottom);
            SetValue(checkBox_side, numericUpDown_side, selectedBoard.MapInfo.LBSide);
            isUpdating = false;
        }

        private static void SetValue(CheckBox option, TextBox input, int? value)
        {
            bool enabled = value.HasValue && value.Value != 0;
            option.IsChecked = enabled;
            input.IsEnabled = enabled;
            input.Text = enabled ? value.Value.ToString() : "0";
        }

        private void BorderOption_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdating || hcsm?.MultiBoard.SelectedBoard == null)
                return;

            TextBox input;
            Action<int?> update;
            if (ReferenceEquals(sender, checkBox_top))
            {
                input = numericUpDown_top;
                update = value => hcsm.MultiBoard.SelectedBoard.MapInfo.LBTop = value;
            }
            else if (ReferenceEquals(sender, checkBox_bottom))
            {
                input = numericUpDown_bottom;
                update = value => hcsm.MultiBoard.SelectedBoard.MapInfo.LBBottom = value;
            }
            else
            {
                input = numericUpDown_side;
                update = value => hcsm.MultiBoard.SelectedBoard.MapInfo.LBSide = value;
            }

            bool enabled = ((CheckBox)sender).IsChecked == true;
            input.IsEnabled = enabled;
            if (!enabled)
            {
                input.Text = "0";
                update(null);
            }
            else if (TryGetValue(input, out int value))
            {
                update(value);
            }
        }

        private void BorderValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdating || hcsm?.MultiBoard.SelectedBoard == null || sender is not TextBox input || !input.IsEnabled)
                return;
            if (!TryGetValue(input, out int value))
                return;

            switch (input.Tag as string)
            {
                case "Top":
                    hcsm.MultiBoard.SelectedBoard.MapInfo.LBTop = value;
                    break;
                case "Bottom":
                    hcsm.MultiBoard.SelectedBoard.MapInfo.LBBottom = value;
                    break;
                case "Side":
                    hcsm.MultiBoard.SelectedBoard.MapInfo.LBSide = value;
                    break;
            }
        }

        private static bool TryGetValue(TextBox input, out int value)
        {
            if (!int.TryParse(input.Text, out value))
                return false;
            value = Math.Clamp(value, -500, 1000);
            return true;
        }
    }
}
