using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class NameInputBox : Window
    {
        private string nameResult;

        public static bool Show(string title, int maxInputLength, out string name)
        {
            NameInputBox form = new(title);
            if (maxInputLength != 0)
                form.nameBox.MaxLength = maxInputLength;
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            return accepted;
        }

        public NameInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "Name:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
        }

        private void NameInputBox_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                nameBox.Focus();
                Keyboard.Focus(nameBox);
                nameBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Accept();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Accept()
        {
            if (string.IsNullOrEmpty(nameBox.Text))
            {
                InputDialogSupport.WarnInvalidInput();
                return;
            }
            nameResult = nameBox.Text;
            DialogResult = true;
        }
    }
}
