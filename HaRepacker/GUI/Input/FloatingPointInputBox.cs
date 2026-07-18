using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class FloatingPointInputBox : Window
    {
        private string nameResult;
        private double? doubleResult;

        public static bool Show(string title, out string name, out double? value)
        {
            FloatingPointInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            value = form.doubleResult;
            return accepted;
        }

        public FloatingPointInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "Name:");
            labelValue.Text = InputDialogSupport.Text(GetType(), "label2.Text", "Value:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Accept()
        {
            if (string.IsNullOrEmpty(nameBox.Text) ||
                !double.TryParse(valueBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
            { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = nameBox.Text;
            doubleResult = value;
            DialogResult = true;
        }
    }
}
