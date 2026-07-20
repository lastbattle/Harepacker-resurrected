using System;
using System.Windows;
using System.Windows.Controls;
using HaSharedLibrary.GUI;

namespace HaCreator.GUI.InfoEditorControls
{
    public class CheckBox : System.Windows.Controls.CheckBox
    {
        public bool Enabled { get => IsEnabled; set => IsEnabled = value; }
    }

    public class NumericUpDown : NumericTextBox
    {
        public decimal Minimum { get; set; } = int.MinValue;
        public decimal Maximum { get; set; } = int.MaxValue;
        public decimal Value
        {
            get => decimal.TryParse(Text, out decimal value) ? Math.Clamp(value, Minimum, Maximum) : 0;
            set => Text = Math.Clamp(value, Minimum, Maximum).ToString(System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    public class CheckListBox : ListBox
    {
        public void AddOption(string text) => Items.Add(new CheckBox { Content = text, Margin = new Thickness(2) });
        public void SetChecked(int index, bool value) => ((CheckBox)Items[index]).IsChecked = value;
        public bool Checked(int index) => ((CheckBox)Items[index]).IsChecked == true;
    }
}
