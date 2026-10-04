using System;
using System.ComponentModel;
using HaCreator.GUI.Localization;
using System.Globalization;
using System.Windows;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI.Input
{
    public sealed class NameValueInput : IDisposable
    {
        public delegate string ValidationCallback(string name, int value);

        private readonly NameValueInputWindow window;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int SelectedValue { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string SelectedName { get; set; }

        public NameValueInput(ValidationCallback validationCallback)
        {
            window = new NameValueInputWindow(validationCallback);
            window.Accepted += (_, result) =>
            {
                SelectedName = result.Name;
                SelectedValue = result.Value;
            };
        }

        public void SetWindowInfo(string overrideNameLabel, string overrideValueLabel, string overrideTitle)
        {
            window.SetWindowInfo(overrideNameLabel, overrideValueLabel, overrideTitle);
        }

        public Forms.DialogResult ShowDialog()
        {
            bool? accepted = window.ShowDialog();
            if (accepted == true)
                return Forms.DialogResult.OK;

            SelectedName = null;
            SelectedValue = 0;
            return Forms.DialogResult.Cancel;
        }

        public void Dispose()
        {
            if (window.IsLoaded)
                window.Close();
        }
    }

    internal partial class NameValueInputWindow : Window
    {
        private readonly NameValueInput.ValidationCallback validationCallback;
        internal event EventHandler<(string Name, int Value)> Accepted;

        internal NameValueInputWindow(NameValueInput.ValidationCallback validationCallback)
        {
            this.validationCallback = validationCallback;
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
        }

        internal void SetWindowInfo(string nameLabel, string valueLabel, string title)
        {
            if (nameLabel != null)
                labelName.Text = nameLabel;
            if (valueLabel != null)
                labelValue.Text = valueLabel;
            if (title != null)
                Title = title;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(valueInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_EnterValidInteger"), DialogTextExtension.Get("Dialog_InvalidInput"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = nameInput.Text;
            string error = validationCallback?.Invoke(name, value) ?? string.Empty;
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(this, error, DialogTextExtension.Get("Dialog_ValidationFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Accepted?.Invoke(this, (name, value));
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
