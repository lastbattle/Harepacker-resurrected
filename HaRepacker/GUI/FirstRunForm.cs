using System.ComponentModel;
using System.Windows;

namespace HaRepacker.GUI
{
    public partial class FirstRunForm : ThemedDialogWindow
    {
        private bool _canClose;

        public FirstRunForm()
        {
            InitializeComponent();
            Title = WpfDialogSupport.Text(typeof(FirstRunForm), "$this.Text", "First Run");
            autoAssociateBox.Content = WpfDialogSupport.Text(typeof(FirstRunForm), "autoAssociateBox.Text", "Automatically associate WZ files with HaRepacker");
            confirmButton.Content = WpfDialogSupport.Text(typeof(FirstRunForm), "button1.Text", "OK");
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Program.ConfigurationManager.UserSettings.AutoAssociate = autoAssociateBox.IsChecked == true;
            _canClose = true;
            DialogResult = true;
        }

        private void FirstRunForm_FormClosing(object sender, CancelEventArgs e) => e.Cancel = !_canClose;
    }
}
