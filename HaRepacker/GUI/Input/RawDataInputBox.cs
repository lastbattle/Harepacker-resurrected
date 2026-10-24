using Microsoft.Win32;
using HaRepacker.GUI;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace HaRepacker.GUI.Input
{
    public partial class RawDataInputBox : Window
    {
        private string nameResult;
        private byte[] rawDataResult;

        public static bool Show(string title, out string name, out byte[] data)
        {
            RawDataInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            data = form.rawDataResult;
            return accepted;
        }

        public RawDataInputBox(string title)
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
                Title = UiLocalization.Translate("Select raw data file"),
                Filter = UiLocalization.Translate("All files (*.*)|*.*")
            };
            if (dialog.ShowDialog(this) == true) pathBox.Text = dialog.FileName;
        }

        private void Accept()
        {
            if (string.IsNullOrEmpty(nameBox.Text) || string.IsNullOrEmpty(pathBox.Text) || !File.Exists(pathBox.Text))
            { InputDialogSupport.WarnInvalidInput(); return; }
            try
            {
                rawDataResult = File.ReadAllBytes(pathBox.Text);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Warning.Error(exception.Message);
                return;
            }
            nameResult = nameBox.Text;
            DialogResult = true;
        }
    }
}
