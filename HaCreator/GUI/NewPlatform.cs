using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.GUI
{
    public partial class NewPlatform : Window
    {
        public int result;
        private readonly SortedSet<int> zms;

        public NewPlatform(SortedSet<int> zms)
        {
            this.zms = zms;
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
            ValidatePlatform();
        }

        private void Platform_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidatePlatform();
        }

        private void Step_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(platformBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int current);
            int delta = ReferenceEquals(sender, incrementButton) ? 1 : -1;
            platformBox.Text = (current + delta).ToString(CultureInfo.InvariantCulture);
            platformBox.SelectAll();
        }

        private void ValidatePlatform()
        {
            if (okButton == null)
                return;

            bool valid = int.TryParse(platformBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
            bool exists = valid && zms.Contains(value);
            statusLabel.Text = !valid ? "Enter a valid integer." : exists ? "Already exists" : string.Empty;
            okButton.IsEnabled = valid && !exists;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(platformBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return;
            DialogResult = true;
        }
    }
}
