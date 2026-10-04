using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class LongInputBox : Window
    {
        private string nameResult;
        private long? intResult;

        public static bool Show(string title, out string name, out long? integer)
        {
            LongInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            integer = form.intResult;
            return accepted;
        }

        public LongInputBox(string title)
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
                !long.TryParse(valueBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out long value))
            { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = nameBox.Text;
            intResult = value;
            DialogResult = true;
        }
    }
}
