using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class SoundInputBox : Window
    {
        private string nameResult;
        private string soundResult;

        public static bool Show(string title, out string name, out string path)
        {
            SoundInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            path = form.soundResult;
            return accepted;
        }

        public SoundInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "Name:");
            labelPath.Text = InputDialogSupport.Text(GetType(), "label2.Text", "Path:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
            browseButton.Content = InputDialogSupport.Text(GetType(), "browseButton.Text", "Browse…");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = Properties.Resources.SelectMp3,
                Filter = $"{Properties.Resources.Mp3Filter}|*.mp3"
            };
            if (dialog.ShowDialog(this) == true) pathBox.Text = dialog.FileName;
        }

        private void Accept()
        {
            if (string.IsNullOrEmpty(nameBox.Text) || string.IsNullOrEmpty(pathBox.Text) || !File.Exists(pathBox.Text))
            { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = nameBox.Text;
            soundResult = pathBox.Text;
            DialogResult = true;
        }
    }
}
