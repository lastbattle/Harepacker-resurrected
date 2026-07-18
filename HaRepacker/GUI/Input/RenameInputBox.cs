using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class RenameInputBox : Window
    {
        private string newNameResult;

        public static bool Show(string title, string previousItemName, out string newName)
        {
            RenameInputBox form = new(title);
            form.nameBox.Text = previousItemName;
            form.nameBox.SelectAll();
            bool accepted = form.ShowDialog() == true;
            newName = form.newNameResult;
            return accepted;
        }

        public RenameInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "New name:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Accept()
        {
            if (string.IsNullOrEmpty(nameBox.Text)) { InputDialogSupport.WarnInvalidInput(); return; }
            newNameResult = nameBox.Text;
            DialogResult = true;
        }
    }
}
