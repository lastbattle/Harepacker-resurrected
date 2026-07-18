using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Linq;
using HaSharedLibrary.GUI;

namespace HaRepacker.GUI
{
    internal static class WpfDialogSupport
    {
        public static string Text(Type dialogType, string key, string fallback)
        {
            ComponentResourceManager resources = new(dialogType);
            return UiLocalization.Translate(resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback);
        }

        public static int ParseInteger(string text, int fallback = 0) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
                ? value
                : fallback;
    }

    public class ThemedDialogWindow : Window, IDisposable
    {
        private bool localizationApplied;

        public string Text
        {
            get => Title;
            set => Title = value;
        }

        public new System.Windows.Forms.DialogResult ShowDialog() =>
            base.ShowDialog() == true
                ? System.Windows.Forms.DialogResult.OK
                : System.Windows.Forms.DialogResult.Cancel;

        public System.Windows.Forms.DialogResult ShowDialog(System.Windows.Forms.IWin32Window owner)
        {
            if (owner != null)
                new WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose()
        {
            if (IsVisible)
                Close();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (localizationApplied)
                return;

            UiLocalization.Apply(this);
            localizationApplied = true;
        }
    }

    public class WpfCheckedListBox : ListBox
    {
        public IEnumerable<object> CheckedItems => Items.OfType<CheckBox>()
            .Where(item => item.IsChecked == true)
            .Select(item => item.Content);

        public void AddItem(string text, bool isChecked)
        {
            Items.Add(new CheckBox { Content = text, IsChecked = isChecked, Padding = new Thickness(2) });
        }

        public void SetItemChecked(int index, bool isChecked)
        {
            if (Items[index] is CheckBox item)
                item.IsChecked = isChecked;
        }
    }

    public class WpfNumericInput : NumericTextBox
    {
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; } = decimal.MaxValue;
        public decimal Value
        {
            get => decimal.TryParse(Text, out decimal value) ? value : Minimum;
            set => Text = Math.Clamp(value, Minimum, Maximum).ToString(CultureInfo.CurrentCulture);
        }
    }
}
