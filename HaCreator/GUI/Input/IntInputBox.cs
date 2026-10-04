using System.Globalization;
using System.ComponentModel;
using System.Windows;

namespace HaCreator.GUI.Input
{
    public partial class IntInputBox : Window
    {
        private bool bHideNameInputBox;
        private string nameResult;
        private int? intResult;

        public static bool Show(string title, string defaultName, int defaultValue,
            out string name, out int? integer, bool bHideNameInputBox = false)
        {
            IntInputBox window = new IntInputBox(title) { bHideNameInputBox = bHideNameInputBox };
            if (bHideNameInputBox)
                window.nameRow.Visibility = Visibility.Collapsed;
            if (defaultName != null)
                window.nameBox.Text = defaultName;
            if (defaultValue != 0)
                window.valueBox.Text = defaultValue.ToString(CultureInfo.InvariantCulture);

            bool accepted = window.ShowDialog() == true;
            name = window.nameResult;
            integer = window.intResult;
            return accepted;
        }

        public IntInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
            ComponentResourceManager resources = new ComponentResourceManager(typeof(IntInputBox));
            labelName.Text = resources.GetString("label_name.Text") ?? "Name:";
            labelValue.Text = resources.GetString("label2.Text") ?? "Value:";
            okButton.Content = resources.GetString("okButton.Text") ?? "OK";
            cancelButton.Content = resources.GetString("cancelButton.Text") ?? "Cancel";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (((!string.IsNullOrEmpty(nameBox.Text)) || bHideNameInputBox) &&
                int.TryParse(valueBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                nameResult = nameBox.Text;
                intResult = value;
                DialogResult = true;
                return;
            }

            MessageBox.Show(this, Properties.Resources.EnterValidInput, Properties.Resources.Warning,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
