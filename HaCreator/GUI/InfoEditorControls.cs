using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        public NumericUpDown()
        {
            LostKeyboardFocus += NumericUpDown_LostKeyboardFocus;
        }

        public decimal Value
        {
            get
            {
                if (!decimal.TryParse(Text, ParsingStyles, CultureInfo.CurrentCulture, out decimal value))
                    value = 0;

                return Math.Clamp(value, Minimum, Maximum);
            }
            set => Text = FormatValue(Math.Clamp(value, Minimum, Maximum));
        }

        public void NormalizeValue()
        {
            Text = FormatValue(Value);
        }

        private void NumericUpDown_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            NormalizeValue();
        }

        private static string FormatValue(decimal value) =>
            value.ToString(CultureInfo.CurrentCulture);

        private NumberStyles ParsingStyles
        {
            get
            {
                NumberStyles styles = AllowDecimal
                    ? NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                    : NumberStyles.AllowLeadingSign;

                if (!AllowNegative)
                    styles &= ~NumberStyles.AllowLeadingSign;

                return styles;
            }
        }

    }

    public class CheckListBox : ListBox
    {
        public void AddOption(string text) => Items.Add(new CheckBox { Content = text, Margin = new Thickness(2) });
        public void SetChecked(int index, bool value) => ((CheckBox)Items[index]).IsChecked = value;
        public bool Checked(int index) => ((CheckBox)Items[index]).IsChecked == true;
    }
}
