using System;
using System.Windows;

namespace HaCreator.GUI.Cutscene
{
    internal partial class CutsceneSceneNameDialog : Window
    {
        private readonly Func<string, string> _validateName;

        public CutsceneSceneNameDialog(string defaultName, Func<string, string> validateName)
        {
            InitializeComponent();
            _validateName = validateName;
            nameTextBox.Text = defaultName ?? string.Empty;
        }

        public string ScenePath => nameTextBox.Text.Trim();

        private void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            nameTextBox.Focus();
            nameTextBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string error = _validateName?.Invoke(ScenePath);
            if (!string.IsNullOrWhiteSpace(error))
            {
                validationTextBlock.Text = error;
                validationTextBlock.Visibility = Visibility.Visible;
                nameTextBox.Focus();
                nameTextBox.SelectAll();
                return;
            }

            DialogResult = true;
        }
    }
}
