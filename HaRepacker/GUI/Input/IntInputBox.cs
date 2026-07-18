using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class IntInputBox : Window
    {
        private bool bHideNameInputBox;
        private string nameResult;
        private int? intResult;

        public static bool Show(string title, string defaultName, int defaultValue,
            out string name, out int? integer, bool bHideNameInputBox = false)
        {
            IntInputBox form = new(title) { bHideNameInputBox = bHideNameInputBox };
            if (bHideNameInputBox)
                form.namePanel.Visibility = Visibility.Collapsed;
            if (defaultName != null)
                form.nameBox.Text = defaultName;
            if (defaultValue != 0)
                form.valueBox.Text = defaultValue.ToString(CultureInfo.CurrentCulture);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            integer = form.intResult;
            return accepted;
        }

        public IntInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label_name.Text", "Name:");
            labelValue.Text = InputDialogSupport.Text(GetType(), "label2.Text", "Value:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Accept()
        {
            if ((!bHideNameInputBox && string.IsNullOrEmpty(nameBox.Text)) ||
                !int.TryParse(valueBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value))
            { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = nameBox.Text;
            intResult = value;
            DialogResult = true;
        }
    }
}
