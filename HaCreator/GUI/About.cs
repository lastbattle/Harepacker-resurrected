using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace HaCreator.GUI
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
        }

        private void About_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Repository_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/lastbattle/Harepacker-resurrected") { UseShellExecute = true });
        }
    }
}
